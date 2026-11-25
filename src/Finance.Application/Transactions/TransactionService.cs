using Finance.Application.Common;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Application.CreditCards;
using Finance.Domain.Common;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using FluentValidation;

namespace Finance.Application.Transactions;

public sealed class TransactionService(
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ICreditCardRepository creditCardRepository,
    IAuthRepository authRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateTransactionCommand> createValidator,
    IValidator<CreateTransferCommand> transferValidator,
    IValidator<UpdateTransactionCommand> updateValidator,
    IValidator<AnticipateInstallmentsCommand> anticipateValidator) : ITransactionService
{
    private const decimal LargeTransactionThreshold = 5000m;

    public async Task<PagedTransactionsDto> GetPagedAsync(
        TransactionFilterQuery query,
        CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        var filter = new TransactionFilter(
            query.Month,
            query.Year,
            query.AccountId,
            query.CreditCardId,
            query.CategoryId,
            query.Type,
            query.Status,
            query.InstallmentGroupId,
            page,
            pageSize);

        (IReadOnlyList<Transaction> items, int totalCount) =
            await transactionRepository.GetPagedAsync(userId, filter, ct);

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedTransactionsDto(
            items.Select(MapTransactionDto).ToArray(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<IReadOnlyList<TransactionDto>> CreateAsync(
        CreateTransactionCommand command,
        CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;

        await ValidateCategoryAsync(userId, command.CategoryId, command.Type, ct);

        List<Transaction> created;

        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        if (command.CreditCardId.HasValue)
        {
            created = await CreateCreditCardTransactionsAsync(userId, command, utcNow, ct);
        }
        else
        {
            created = await CreateAccountTransactionsAsync(userId, command, utcNow, ct);
        }

        if (command.Amount >= LargeTransactionThreshold)
        {
            await authRepository.AddAuditLogAsync(
                ApplicationContextHelper.CreateAuditLog(
                    userId,
                    SecurityAuditEventType.LargeTransactionCreated,
                    currentUserService,
                    utcNow,
                    new
                    {
                        amount = decimal.Round(command.Amount, 2, MidpointRounding.AwayFromZero),
                        type = command.Type.ToScreamingSnakeCase(),
                        installments = command.Installments,
                        isCreditCard = command.CreditCardId.HasValue
                    }),
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);

        return created.Select(MapTransactionDto).ToArray();
    }

    public async Task<TransferResultDto> TransferAsync(
        CreateTransferCommand command,
        CancellationToken ct = default)
    {
        await transferValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;

        Account source = await RequireActiveAccountAsync(userId, command.SourceAccountId, ct);
        Account destination = await RequireActiveAccountAsync(userId, command.DestinationAccountId, ct);

        decimal amount = decimal.Round(command.Amount, 2, MidpointRounding.AwayFromZero);

        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        var outgoing = new Transaction(
            userId,
            source.Id,
            null,
            null,
            TransactionType.TransferOut,
            TransactionStatus.Confirmed,
            command.Description,
            amount,
            command.TransferDate,
            utcNow,
            notes: command.Notes);

        var incoming = new Transaction(
            userId,
            destination.Id,
            null,
            null,
            TransactionType.TransferIn,
            TransactionStatus.Confirmed,
            command.Description,
            amount,
            command.TransferDate,
            utcNow,
            notes: command.Notes);

        source.ApplyBalanceChange(-amount);
        destination.ApplyBalanceChange(amount);

        await transactionRepository.AddRangeAsync([outgoing, incoming], ct);
        await unitOfWork.SaveChangesAsync(ct);

        outgoing.LinkTransferPair(incoming.Id);
        incoming.LinkTransferPair(outgoing.Id);

        if (amount >= LargeTransactionThreshold)
        {
            await authRepository.AddAuditLogAsync(
                ApplicationContextHelper.CreateAuditLog(
                    userId,
                    SecurityAuditEventType.LargeTransactionCreated,
                    currentUserService,
                    utcNow,
                    new
                    {
                        amount,
                        type = TransactionType.TransferOut.ToScreamingSnakeCase(),
                        sourceAccountId = source.Id,
                        destinationAccountId = destination.Id
                    }),
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);

        return new TransferResultDto(MapTransactionDto(outgoing), MapTransactionDto(incoming));
    }

    public async Task<TransactionDto> UpdateAsync(
        Guid id,
        UpdateTransactionCommand command,
        CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        Transaction tx = await transactionRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("transaction_not_found", "Lançamento não encontrado.");

        if (tx.Type == TransactionType.InvoicePayment)
        {
            throw AppException.Unprocessable(
                "invoice_payment_immutable",
                "Lançamentos de pagamento de fatura não podem ser editados diretamente.");
        }

        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        decimal newAmount = decimal.Round(command.Amount, 2, MidpointRounding.AwayFromZero);

        if (tx.Type is TransactionType.TransferIn or TransactionType.TransferOut)
        {
            if (!tx.TransferPairId.HasValue)
            {
                throw AppException.Unprocessable("invalid_transfer_pair", "Par da transferência não encontrado.");
            }

            Transaction pair = await transactionRepository.GetByIdAsync(userId, tx.TransferPairId.Value, ct)
                ?? throw AppException.NotFound("transaction_not_found", "Par da transferência não encontrado.");

            Transaction outTx = tx.Type == TransactionType.TransferOut ? tx : pair;
            Transaction inTx = tx.Type == TransactionType.TransferIn ? tx : pair;

            Account source = await RequireAccountAsync(userId, outTx.AccountId!.Value, ct);
            Account destination = await RequireAccountAsync(userId, inTx.AccountId!.Value, ct);

            decimal diff = newAmount - outTx.Amount;
            source.ApplyBalanceChange(-diff);
            destination.ApplyBalanceChange(diff);

            outTx.UpdateDetails(command.Description, newAmount, command.TransactionDate, null, command.Notes);
            inTx.UpdateDetails(command.Description, newAmount, command.TransactionDate, null, command.Notes);
        }
        else if (tx.CreditCardInvoiceId.HasValue)
        {
            await ValidateCategoryAsync(userId, command.CategoryId, tx.Type, ct);

            CreditCardInvoice invoice = await creditCardRepository.GetInvoiceByIdDirectAsync(tx.CreditCardInvoiceId.Value, ct)
                ?? throw AppException.NotFound("invoice_not_found", "Fatura não encontrada.");

            if (invoice.Status == InvoiceStatus.Paid)
            {
                throw AppException.Unprocessable(
                    "invoice_already_paid",
                    "Não é possível alterar lançamentos de uma fatura já paga.");
            }

            invoice.RemoveCharge(tx.Amount);
            invoice.AddCharge(newAmount);
            tx.UpdateDetails(command.Description, newAmount, command.TransactionDate, command.CategoryId, command.Notes);
        }
        else if (tx.AccountId.HasValue)
        {
            await ValidateCategoryAsync(userId, command.CategoryId, tx.Type, ct);

            Account account = await RequireAccountAsync(userId, tx.AccountId.Value, ct);
            if (tx.Status == TransactionStatus.Confirmed)
            {
                ApplySignedDelta(account, tx.Type, -tx.Amount);
                ApplySignedDelta(account, tx.Type, newAmount);
            }

            tx.UpdateDetails(command.Description, newAmount, command.TransactionDate, command.CategoryId, command.Notes);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);

        return MapTransactionDto(tx);
    }

    public async Task<TransactionDto> ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        Transaction tx = await transactionRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("transaction_not_found", "Lançamento não encontrado.");

        if (!tx.AccountId.HasValue || tx.CreditCardInvoiceId.HasValue)
        {
            throw AppException.Unprocessable(
                "confirmation_not_applicable",
                "Apenas lançamentos em conta podem ser confirmados.");
        }

        Account account = await RequireActiveAccountAsync(userId, tx.AccountId.Value, ct);

        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        tx.Confirm();
        ApplySignedDelta(account, tx.Type, tx.Amount);

        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);

        return MapTransactionDto(tx);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        Transaction tx = await transactionRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("transaction_not_found", "Lançamento não encontrado.");

        if (tx.Type == TransactionType.InvoicePayment)
        {
            throw AppException.Unprocessable(
                "invoice_payment_immutable",
                "Lançamentos de pagamento de fatura não podem ser excluídos diretamente.");
        }

        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        if (tx.Type is TransactionType.TransferIn or TransactionType.TransferOut)
        {
            if (tx.TransferPairId.HasValue)
            {
                Transaction? pair = await transactionRepository.GetByIdAsync(userId, tx.TransferPairId.Value, ct);
                if (pair is not null)
                {
                    Account accountA = await RequireAccountAsync(userId, tx.AccountId!.Value, ct);
                    Account accountB = await RequireAccountAsync(userId, pair.AccountId!.Value, ct);

                    ApplySignedDelta(accountA, tx.Type, -tx.Amount);
                    ApplySignedDelta(accountB, pair.Type, -pair.Amount);

                    tx.UnlinkTransferPair();
                    pair.UnlinkTransferPair();
                    await unitOfWork.SaveChangesAsync(ct);

                    transactionRepository.RemoveRange([tx, pair]);
                }
                else
                {
                    Account account = await RequireAccountAsync(userId, tx.AccountId!.Value, ct);
                    ApplySignedDelta(account, tx.Type, -tx.Amount);
                    transactionRepository.Remove(tx);
                }
            }
            else
            {
                Account account = await RequireAccountAsync(userId, tx.AccountId!.Value, ct);
                ApplySignedDelta(account, tx.Type, -tx.Amount);
                transactionRepository.Remove(tx);
            }
        }
        else if (tx.CreditCardInvoiceId.HasValue)
        {
            CreditCardInvoice invoice = await creditCardRepository.GetInvoiceByIdDirectAsync(tx.CreditCardInvoiceId.Value, ct)
                ?? throw AppException.NotFound("invoice_not_found", "Fatura não encontrada.");

            if (invoice.Status == InvoiceStatus.Paid)
            {
                throw AppException.Unprocessable(
                    "invoice_already_paid",
                    "Não é possível excluir lançamentos de uma fatura já paga.");
            }

            invoice.RemoveCharge(tx.Amount);
            transactionRepository.Remove(tx);
        }
        else if (tx.AccountId.HasValue)
        {
            if (tx.Status == TransactionStatus.Confirmed)
            {
                Account account = await RequireAccountAsync(userId, tx.AccountId.Value, ct);
                ApplySignedDelta(account, tx.Type, -tx.Amount);
            }

            transactionRepository.Remove(tx);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<InstallmentGroupSummaryDto>> GetInstallmentGroupsAsync(
        bool includeCompleted,
        CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        IReadOnlyList<Transaction> allInstallmentTxs =
            await transactionRepository.GetAllInstallmentTransactionsAsync(userId, ct);

        if (allInstallmentTxs.Count == 0)
            return [];

        Guid[] invoiceIds = allInstallmentTxs
            .Where(t => t.CreditCardInvoiceId.HasValue)
            .Select(t => t.CreditCardInvoiceId!.Value)
            .Distinct()
            .ToArray();

        Dictionary<Guid, CreditCardInvoice> invoicesById = invoiceIds.Length == 0
            ? new Dictionary<Guid, CreditCardInvoice>()
            : (await creditCardRepository.GetInvoicesByIdsAsync(invoiceIds, ct))
                .ToDictionary(i => i.Id);

        DateOnly today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var summaries = new List<InstallmentGroupSummaryDto>();
        foreach (IGrouping<Guid, Transaction> group in allInstallmentTxs.GroupBy(t => t.InstallmentGroupId!.Value))
        {
            InstallmentGroupDetailDto detail = BuildInstallmentGroupDetail(group.Key, group.ToList(), invoicesById, today);
            if (!includeCompleted && detail.RemainingCount == 0)
                continue;

            summaries.Add(new InstallmentGroupSummaryDto(
                detail.InstallmentGroupId,
                detail.SourceKind,
                detail.CreditCardId,
                detail.AccountId,
                detail.CategoryId,
                detail.Type,
                detail.Description,
                detail.TotalInstallments,
                detail.SettledCount,
                detail.RemainingCount,
                detail.CanAnticipateCount,
                detail.TotalOriginalAmount,
                detail.TotalCurrentAmount,
                detail.RemainingAmount,
                detail.TotalDiscountSaved,
                detail.NextDueDate));
        }

        return summaries
            .OrderBy(s => s.NextDueDate ?? DateOnly.MaxValue)
            .ThenBy(s => s.Description, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<InstallmentGroupDetailDto> GetInstallmentGroupDetailAsync(
        Guid groupId,
        CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        IReadOnlyList<Transaction> groupTxs =
            await transactionRepository.GetByInstallmentGroupAsync(userId, groupId, ct);

        if (groupTxs.Count == 0)
        {
            throw AppException.NotFound("installment_group_not_found", "Grupo de parcelamento não encontrado.");
        }

        Guid[] invoiceIds = groupTxs
            .Where(t => t.CreditCardInvoiceId.HasValue)
            .Select(t => t.CreditCardInvoiceId!.Value)
            .Distinct()
            .ToArray();

        Dictionary<Guid, CreditCardInvoice> invoicesById = invoiceIds.Length == 0
            ? new Dictionary<Guid, CreditCardInvoice>()
            : (await creditCardRepository.GetInvoicesByIdsAsync(invoiceIds, ct))
                .ToDictionary(i => i.Id);

        DateOnly today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        return BuildInstallmentGroupDetail(groupId, groupTxs, invoicesById, today);
    }

    public async Task<AnticipationResultDto> AnticipateInstallmentsAsync(
        Guid groupId,
        AnticipateInstallmentsCommand command,
        CancellationToken ct = default)
    {
        await anticipateValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        IReadOnlyList<Transaction> groupTxs =
            await transactionRepository.GetByInstallmentGroupAsync(userId, groupId, ct);

        if (groupTxs.Count == 0)
        {
            throw AppException.NotFound("installment_group_not_found", "Grupo de parcelamento não encontrado.");
        }

        Guid[] invoiceIds = groupTxs
            .Where(t => t.CreditCardInvoiceId.HasValue)
            .Select(t => t.CreditCardInvoiceId!.Value)
            .Distinct()
            .ToArray();

        Dictionary<Guid, CreditCardInvoice> invoicesById = invoiceIds.Length == 0
            ? new Dictionary<Guid, CreditCardInvoice>()
            : (await creditCardRepository.GetInvoicesByIdsAsync(invoiceIds, ct))
                .ToDictionary(i => i.Id);

        bool isCardGroup = invoiceIds.Length > 0;
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        DateOnly today = DateOnly.FromDateTime(utcNow.UtcDateTime);
        DateOnly effectiveDate = command.PaymentDate ?? today;

        if (!isCardGroup)
        {
            if (command.Mode == AnticipationMode.MoveToInvoice)
            {
                throw AppException.Unprocessable(
                    "invalid_anticipation_mode",
                    "Parcelamentos em conta devem ser antecipados via conta bancária (PAY_FROM_ACCOUNT).");
            }

            return await AnticipateAccountGroupAsync(
                userId,
                groupId,
                groupTxs,
                command,
                effectiveDate,
                utcNow,
                ct);
        }

        Guid cardId = invoicesById.Values.First().CreditCardId;
        CreditCard card = await creditCardRepository.GetByIdAsync(userId, cardId, ct)
            ?? throw AppException.NotFound("credit_card_not_found", "Cartão de crédito não encontrado.");

        if (card.IsArchived)
        {
            throw AppException.Unprocessable("credit_card_archived", "O cartão deste parcelamento está arquivado.");
        }

        AnticipationMode mode = command.Mode ?? AnticipationMode.MoveToInvoice;

        if (mode == AnticipationMode.MoveToInvoice)
        {
            return await AnticipateCardToInvoiceAsync(
                userId,
                groupId,
                card,
                groupTxs,
                invoicesById,
                command,
                today,
                effectiveDate,
                utcNow,
                ct);
        }

        return await AnticipateCardFromAccountAsync(
            userId,
            groupId,
            card,
            groupTxs,
            invoicesById,
            command,
            effectiveDate,
            utcNow,
            ct);
    }

    private async Task<List<Transaction>> CreateCreditCardTransactionsAsync(
        Guid userId,
        CreateTransactionCommand command,
        DateTimeOffset utcNow,
        CancellationToken ct)
    {
        CreditCard card = await creditCardRepository.GetByIdAsync(userId, command.CreditCardId!.Value, ct)
            ?? throw AppException.NotFound("credit_card_not_found", "Cartão de crédito não encontrado.");

        if (card.IsArchived)
        {
            throw AppException.Unprocessable("credit_card_archived", "O cartão selecionado está arquivado.");
        }

        IReadOnlyList<decimal> installmentAmounts = CreditCard.SplitInstallments(command.Amount, command.Installments);
        Guid? groupId = command.Installments > 1 ? Guid.CreateVersion7() : null;
        (int startYear, int startMonth) = card.CalculateInvoiceReference(command.TransactionDate);
        var startReference = new DateOnly(startYear, startMonth, 1);

        var invoiceCache = new Dictionary<(int Year, int Month), CreditCardInvoice>();
        var created = new List<Transaction>(command.Installments);

        for (int i = 0; i < command.Installments; i++)
        {
            DateOnly reference = startReference.AddMonths(i);
            CreditCardInvoice invoice = await GetOrCreateInvoiceAsync(
                card,
                reference.Year,
                reference.Month,
                invoiceCache,
                ct);

            if (invoice.Status == InvoiceStatus.Paid)
            {
                throw AppException.Unprocessable(
                    "invoice_already_paid",
                    $"A fatura {reference.Month:D2}/{reference.Year} já foi paga.");
            }

            decimal partAmount = installmentAmounts[i];
            invoice.AddCharge(partAmount);

            DateOnly installmentDate = i == 0
                ? command.TransactionDate
                : command.TransactionDate.AddMonths(i);

            var tx = new Transaction(
                userId,
                null,
                invoice.Id,
                command.CategoryId,
                TransactionType.Expense,
                TransactionStatus.Confirmed,
                command.Description,
                partAmount,
                installmentDate,
                utcNow,
                installmentNumber: command.Installments > 1 ? i + 1 : null,
                totalInstallments: command.Installments > 1 ? command.Installments : null,
                notes: command.Notes,
                installmentGroupId: groupId);

            created.Add(tx);
        }

        await transactionRepository.AddRangeAsync(created, ct);
        return created;
    }

    private async Task<List<Transaction>> CreateAccountTransactionsAsync(
        Guid userId,
        CreateTransactionCommand command,
        DateTimeOffset utcNow,
        CancellationToken ct)
    {
        Account account = await RequireActiveAccountAsync(userId, command.AccountId!.Value, ct);
        var created = new List<Transaction>(command.Installments);

        if (command.Installments == 1)
        {
            TransactionStatus status = command.Status ?? TransactionStatus.Confirmed;
            decimal amount = decimal.Round(command.Amount, 2, MidpointRounding.AwayFromZero);

            var tx = new Transaction(
                userId,
                account.Id,
                null,
                command.CategoryId,
                command.Type,
                status,
                command.Description,
                amount,
                command.TransactionDate,
                utcNow,
                notes: command.Notes);

            if (status == TransactionStatus.Confirmed)
            {
                ApplySignedDelta(account, command.Type, amount);
            }

            created.Add(tx);
            await transactionRepository.AddAsync(tx, ct);
            return created;
        }

        IReadOnlyList<decimal> installmentAmounts = CreditCard.SplitInstallments(command.Amount, command.Installments);
        Guid groupId = Guid.CreateVersion7();
        TransactionStatus firstStatus = command.Status ?? TransactionStatus.Pending;

        for (int i = 0; i < command.Installments; i++)
        {
            TransactionStatus status = i == 0 ? firstStatus : TransactionStatus.Pending;
            decimal partAmount = installmentAmounts[i];

            var tx = new Transaction(
                userId,
                account.Id,
                null,
                command.CategoryId,
                command.Type,
                status,
                command.Description,
                partAmount,
                command.TransactionDate.AddMonths(i),
                utcNow,
                installmentNumber: i + 1,
                totalInstallments: command.Installments,
                notes: command.Notes,
                installmentGroupId: groupId);

            if (status == TransactionStatus.Confirmed)
            {
                ApplySignedDelta(account, command.Type, partAmount);
            }

            created.Add(tx);
        }

        await transactionRepository.AddRangeAsync(created, ct);
        return created;
    }

    private async Task<AnticipationResultDto> AnticipateCardToInvoiceAsync(
        Guid userId,
        Guid groupId,
        CreditCard card,
        IReadOnlyList<Transaction> groupTxs,
        Dictionary<Guid, CreditCardInvoice> invoicesById,
        AnticipateInstallmentsCommand command,
        DateOnly today,
        DateOnly anticipatedDate,
        DateTimeOffset utcNow,
        CancellationToken ct)
    {
        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        CreditCardInvoice targetInvoice = await ResolveTargetOpenInvoiceAsync(
            userId,
            card,
            command.TargetInvoiceId,
            today,
            invoicesById,
            ct);

        bool IsEligible(Transaction t)
        {
            if (!t.CreditCardInvoiceId.HasValue || t.AnticipatedAtUtc.HasValue)
                return false;

            CreditCardInvoice src = invoicesById[t.CreditCardInvoiceId.Value];
            return src.Status != InvoiceStatus.Paid && IsStrictlyAfterInvoice(src, targetInvoice);
        }

        List<Transaction> selected = SelectInstallments(
            groupTxs,
            IsEligible,
            command,
            ineligibleReason: "Somente parcelas não antecipadas de faturas futuras podem ser trazidas para a fatura destino.");

        decimal[] originalAmounts = selected.Select(t => t.Amount).ToArray();
        IReadOnlyList<decimal> netAmounts = InstallmentMath.ApplyDiscount(originalAmounts, command.DiscountAmount);

        for (int i = 0; i < selected.Count; i++)
        {
            Transaction tx = selected[i];
            decimal netAmount = netAmounts[i];
            CreditCardInvoice sourceInvoice = invoicesById[tx.CreditCardInvoiceId!.Value];

            sourceInvoice.RemoveCharge(tx.Amount);
            targetInvoice.AddCharge(netAmount);
            tx.AnticipateToInvoice(targetInvoice.Id, anticipatedDate, netAmount, utcNow);
        }

        invoicesById[targetInvoice.Id] = targetInvoice;

        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);

        return BuildAnticipationResult(
            groupId,
            AnticipationMode.MoveToInvoice,
            selected,
            originalAmounts,
            command.DiscountAmount,
            netAmounts,
            targetInvoice.Id,
            paymentAccountId: null,
            invoicesById,
            today);
    }

    private async Task<AnticipationResultDto> AnticipateCardFromAccountAsync(
        Guid userId,
        Guid groupId,
        CreditCard card,
        IReadOnlyList<Transaction> groupTxs,
        Dictionary<Guid, CreditCardInvoice> invoicesById,
        AnticipateInstallmentsCommand command,
        DateOnly paymentDate,
        DateTimeOffset utcNow,
        CancellationToken ct)
    {
        Guid paymentAccountId = command.PaymentAccountId
            ?? card.DefaultAccountId
            ?? throw AppException.Unprocessable(
                "payment_account_required",
                "Informe a conta pagadora ou configure uma conta padrão no cartão.");

        Account paymentAccount = await RequireActiveAccountAsync(userId, paymentAccountId, ct);

        bool IsEligible(Transaction t)
            => t.CreditCardInvoiceId.HasValue
               && !t.AnticipatedAtUtc.HasValue
               && invoicesById[t.CreditCardInvoiceId.Value].Status != InvoiceStatus.Paid;

        List<Transaction> selected = SelectInstallments(
            groupTxs,
            IsEligible,
            command,
            ineligibleReason: "Somente parcelas ainda não antecipadas de faturas em aberto/fechadas podem ser quitadas.");

        decimal[] originalAmounts = selected.Select(t => t.Amount).ToArray();
        IReadOnlyList<decimal> netAmounts = InstallmentMath.ApplyDiscount(originalAmounts, command.DiscountAmount);
        decimal netTotal = netAmounts.Sum();

        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        for (int i = 0; i < selected.Count; i++)
        {
            Transaction tx = selected[i];
            CreditCardInvoice sourceInvoice = invoicesById[tx.CreditCardInvoiceId!.Value];

            sourceInvoice.RemoveCharge(tx.Amount);
            tx.AnticipateFromAccount(paymentAccount.Id, paymentDate, netAmounts[i], utcNow);
        }

        ApplySignedDelta(paymentAccount, TransactionType.Expense, netTotal);

        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);

        DateOnly today = DateOnly.FromDateTime(utcNow.UtcDateTime);
        return BuildAnticipationResult(
            groupId,
            AnticipationMode.PayFromAccount,
            selected,
            originalAmounts,
            command.DiscountAmount,
            netAmounts,
            targetInvoiceId: null,
            paymentAccount.Id,
            invoicesById,
            today);
    }

    private async Task<AnticipationResultDto> AnticipateAccountGroupAsync(
        Guid userId,
        Guid groupId,
        IReadOnlyList<Transaction> groupTxs,
        AnticipateInstallmentsCommand command,
        DateOnly paymentDate,
        DateTimeOffset utcNow,
        CancellationToken ct)
    {
        Guid? groupAccountId = groupTxs.Select(t => t.AccountId).FirstOrDefault(id => id.HasValue);
        Guid paymentAccountId = command.PaymentAccountId
            ?? groupAccountId
            ?? throw AppException.Unprocessable("payment_account_required", "Informe a conta pagadora para antecipação.");

        Account paymentAccount = await RequireActiveAccountAsync(userId, paymentAccountId, ct);

        bool IsEligible(Transaction t)
            => !t.CreditCardInvoiceId.HasValue
               && t.AccountId.HasValue
               && t.Status == TransactionStatus.Pending
               && !t.AnticipatedAtUtc.HasValue;

        List<Transaction> selected = SelectInstallments(
            groupTxs,
            IsEligible,
            command,
            ineligibleReason: "Somente parcelas pendentes e ainda não antecipadas podem ser quitadas antecipadamente.");

        decimal[] originalAmounts = selected.Select(t => t.Amount).ToArray();
        IReadOnlyList<decimal> netAmounts = InstallmentMath.ApplyDiscount(originalAmounts, command.DiscountAmount);
        decimal netTotal = netAmounts.Sum();
        TransactionType groupType = groupTxs[0].Type;

        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        for (int i = 0; i < selected.Count; i++)
        {
            selected[i].AnticipateFromAccount(paymentAccount.Id, paymentDate, netAmounts[i], utcNow);
        }

        ApplySignedDelta(paymentAccount, groupType, netTotal);

        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);

        DateOnly today = DateOnly.FromDateTime(utcNow.UtcDateTime);
        return BuildAnticipationResult(
            groupId,
            AnticipationMode.PayFromAccount,
            selected,
            originalAmounts,
            command.DiscountAmount,
            netAmounts,
            targetInvoiceId: null,
            paymentAccount.Id,
            new Dictionary<Guid, CreditCardInvoice>(),
            today);
    }

    private static List<Transaction> SelectInstallments(
        IReadOnlyList<Transaction> groupTxs,
        Func<Transaction, bool> isEligible,
        AnticipateInstallmentsCommand command,
        string ineligibleReason)
    {
        if (command.TransactionIds is { Count: > 0 })
        {
            Dictionary<Guid, Transaction> byId = groupTxs.ToDictionary(t => t.Id);
            var selected = new List<Transaction>(command.TransactionIds.Count);

            foreach (Guid txId in command.TransactionIds)
            {
                if (!byId.TryGetValue(txId, out Transaction? tx))
                {
                    throw AppException.Unprocessable(
                        "installment_not_in_group",
                        "Uma ou mais parcelas selecionadas não pertencem a este parcelamento.");
                }

                if (!isEligible(tx))
                {
                    throw AppException.Unprocessable("installment_not_eligible", ineligibleReason);
                }

                selected.Add(tx);
            }

            return selected.OrderBy(t => t.InstallmentNumber ?? 0).ToList();
        }

        if (command.InstallmentNumbers is { Count: > 0 })
        {
            Dictionary<int, Transaction> byNumber = groupTxs
                .Where(t => t.InstallmentNumber.HasValue)
                .ToDictionary(t => t.InstallmentNumber!.Value);

            var selected = new List<Transaction>(command.InstallmentNumbers.Count);
            foreach (int num in command.InstallmentNumbers)
            {
                if (!byNumber.TryGetValue(num, out Transaction? tx))
                {
                    throw AppException.Unprocessable(
                        "installment_not_in_group",
                        $"Parcela #{num} não encontrada neste parcelamento.");
                }

                if (!isEligible(tx))
                {
                    throw AppException.Unprocessable("installment_not_eligible", ineligibleReason);
                }

                selected.Add(tx);
            }

            return selected.OrderBy(t => t.InstallmentNumber ?? 0).ToList();
        }

        List<Transaction> eligible = groupTxs.Where(isEligible).ToList();
        int count = command.Count!.Value;

        if (eligible.Count < count)
        {
            throw AppException.Unprocessable(
                "insufficient_eligible_installments",
                eligible.Count == 0
                    ? "Não há parcelas elegíveis disponíveis para antecipação."
                    : $"Há apenas {eligible.Count} parcela(s) elegível(eis) disponível(eis) para antecipação.");
        }

        IEnumerable<Transaction> ordered = command.SelectionOrder == InstallmentSelectionOrder.FromLast
            ? eligible.OrderByDescending(t => t.InstallmentNumber ?? 0)
            : eligible.OrderBy(t => t.InstallmentNumber ?? 0);

        return ordered.Take(count).OrderBy(t => t.InstallmentNumber ?? 0).ToList();
    }

    private async Task<CreditCardInvoice> ResolveTargetOpenInvoiceAsync(
        Guid userId,
        CreditCard card,
        Guid? targetInvoiceId,
        DateOnly today,
        IDictionary<Guid, CreditCardInvoice> invoicesById,
        CancellationToken ct)
    {
        if (targetInvoiceId.HasValue)
        {
            CreditCardInvoice explicitTarget = await creditCardRepository.GetInvoiceByIdAsync(
                    userId,
                    card.Id,
                    targetInvoiceId.Value,
                    ct)
                ?? throw AppException.NotFound("invoice_not_found", "Fatura de destino não encontrada.");

            if (explicitTarget.Status != InvoiceStatus.Open || today >= explicitTarget.ClosingDate)
            {
                throw AppException.Unprocessable(
                    "target_invoice_not_open",
                    "A fatura de destino precisa estar aberta para receber parcelas antecipadas.");
            }

            return explicitTarget;
        }

        (int startYear, int startMonth) = card.CalculateInvoiceReference(today);
        var cursor = new DateOnly(startYear, startMonth, 1);

        for (int offset = 0; offset < 24; offset++)
        {
            DateOnly candidateRef = cursor.AddMonths(offset);
            CreditCardInvoice? existing = invoicesById.Values.FirstOrDefault(i =>
                i.CreditCardId == card.Id &&
                i.ReferenceYear == candidateRef.Year &&
                i.ReferenceMonth == candidateRef.Month);

            existing ??= await creditCardRepository.GetInvoiceByReferenceAsync(
                card.Id,
                candidateRef.Year,
                candidateRef.Month,
                ct);

            if (existing is null)
            {
                (DateOnly closingDate, DateOnly dueDate) = card.CalculateInvoiceDates(candidateRef.Year, candidateRef.Month);
                var created = new CreditCardInvoice(card.Id, candidateRef.Year, candidateRef.Month, closingDate, dueDate);
                await creditCardRepository.AddInvoiceAsync(created, ct);
                return created;
            }

            if (existing.Status == InvoiceStatus.Open && today < existing.ClosingDate)
            {
                return existing;
            }
        }

        throw AppException.Unprocessable("target_invoice_not_found", "Não foi possível localizar uma fatura aberta.");
    }

    private async Task<CreditCardInvoice> GetOrCreateInvoiceAsync(
        CreditCard card,
        int year,
        int month,
        IDictionary<(int Year, int Month), CreditCardInvoice> cache,
        CancellationToken ct)
    {
        if (cache.TryGetValue((year, month), out CreditCardInvoice? cached))
            return cached;

        CreditCardInvoice? existing = await creditCardRepository.GetInvoiceByReferenceAsync(card.Id, year, month, ct);
        if (existing is not null)
        {
            cache[(year, month)] = existing;
            return existing;
        }

        (DateOnly closingDate, DateOnly dueDate) = card.CalculateInvoiceDates(year, month);
        var created = new CreditCardInvoice(card.Id, year, month, closingDate, dueDate);
        await creditCardRepository.AddInvoiceAsync(created, ct);
        cache[(year, month)] = created;
        return created;
    }

    private static InstallmentGroupDetailDto BuildInstallmentGroupDetail(
        Guid groupId,
        IReadOnlyList<Transaction> transactions,
        IReadOnlyDictionary<Guid, CreditCardInvoice> invoicesById,
        DateOnly today)
    {
        List<Transaction> ordered = transactions
            .OrderBy(t => t.InstallmentNumber ?? 0)
            .ThenBy(t => t.TransactionDate)
            .ToList();

        Transaction first = ordered[0];
        CreditCardInvoice? firstCardInvoice = ordered
            .Where(t => t.CreditCardInvoiceId.HasValue && invoicesById.ContainsKey(t.CreditCardInvoiceId.Value))
            .Select(t => invoicesById[t.CreditCardInvoiceId!.Value])
            .FirstOrDefault();

        Guid? cardId = firstCardInvoice?.CreditCardId;
        string sourceKind = cardId.HasValue ? "CREDIT_CARD" : (ordered.Any(t => t.CreditCardInvoiceId.HasValue) ? "CREDIT_CARD" : "ACCOUNT");

        Guid? accountId = sourceKind == "ACCOUNT"
            ? ordered.Select(t => t.AccountId).FirstOrDefault(id => id.HasValue)
            : null;

        int settledCount = 0;
        int canAnticipateCount = 0;
        decimal totalOriginal = 0m;
        decimal totalCurrent = 0m;
        decimal remainingAmount = 0m;
        DateOnly? nextDueDate = null;

        var items = new List<InstallmentItemDto>(ordered.Count);

        foreach (Transaction tx in ordered)
        {
            CreditCardInvoice? invoice = tx.CreditCardInvoiceId.HasValue
                ? invoicesById.GetValueOrDefault(tx.CreditCardInvoiceId.Value)
                : null;

            bool isSettled = invoice is not null
                ? invoice.Status == InvoiceStatus.Paid || tx.AnticipatedAtUtc.HasValue
                : tx.Status == TransactionStatus.Confirmed;

            bool canAnticipate = invoice is not null
                ? !tx.AnticipatedAtUtc.HasValue && invoice.Status != InvoiceStatus.Paid
                : !tx.CreditCardInvoiceId.HasValue && tx.AccountId.HasValue && tx.Status == TransactionStatus.Pending && !tx.AnticipatedAtUtc.HasValue;

            if (isSettled)
            {
                settledCount++;
            }
            else
            {
                remainingAmount += tx.Amount;
                DateOnly dueRef = invoice?.DueDate ?? tx.TransactionDate;
                if (!nextDueDate.HasValue || dueRef < nextDueDate.Value)
                    nextDueDate = dueRef;
            }

            if (canAnticipate)
                canAnticipateCount++;

            totalOriginal += tx.OriginalAmount ?? tx.Amount;
            totalCurrent += tx.Amount;

            string? invoiceStatus = invoice is null
                ? null
                : CreditCardService.ComputeEffectiveStatus(invoice, today).ToScreamingSnakeCase();

            items.Add(new InstallmentItemDto(
                tx.Id,
                tx.InstallmentNumber ?? 1,
                tx.TotalInstallments ?? ordered.Count,
                tx.Amount,
                tx.OriginalAmount,
                tx.TransactionDate,
                tx.Status.ToScreamingSnakeCase(),
                tx.AccountId,
                tx.CreditCardInvoiceId,
                invoice?.ReferenceYear,
                invoice?.ReferenceMonth,
                invoiceStatus,
                tx.AnticipatedAtUtc,
                canAnticipate));
        }

        int remainingCount = ordered.Count - settledCount;
        decimal totalDiscountSaved = totalOriginal - totalCurrent;

        return new InstallmentGroupDetailDto(
            groupId,
            sourceKind,
            cardId,
            accountId,
            first.CategoryId,
            first.Type.ToScreamingSnakeCase(),
            first.Description,
            first.TotalInstallments ?? ordered.Count,
            settledCount,
            remainingCount,
            canAnticipateCount,
            totalOriginal,
            totalCurrent,
            remainingAmount,
            totalDiscountSaved,
            nextDueDate,
            items);
    }

    private static AnticipationResultDto BuildAnticipationResult(
        Guid groupId,
        AnticipationMode mode,
        IReadOnlyList<Transaction> updated,
        decimal[] originalAmounts,
        decimal discountAmount,
        IReadOnlyList<decimal> netAmounts,
        Guid? targetInvoiceId,
        Guid? paymentAccountId,
        IReadOnlyDictionary<Guid, CreditCardInvoice> invoicesById,
        DateOnly today)
    {
        decimal roundedDiscount = decimal.Round(discountAmount, 2, MidpointRounding.AwayFromZero);

        IReadOnlyList<InstallmentItemDto> items = updated
            .Select(tx =>
            {
                CreditCardInvoice? inv = tx.CreditCardInvoiceId.HasValue
                    ? invoicesById.GetValueOrDefault(tx.CreditCardInvoiceId.Value)
                    : null;

                return new InstallmentItemDto(
                    tx.Id,
                    tx.InstallmentNumber ?? 1,
                    tx.TotalInstallments ?? updated.Count,
                    tx.Amount,
                    tx.OriginalAmount,
                    tx.TransactionDate,
                    tx.Status.ToScreamingSnakeCase(),
                    tx.AccountId,
                    tx.CreditCardInvoiceId,
                    inv?.ReferenceYear,
                    inv?.ReferenceMonth,
                    inv is null ? null : CreditCardService.ComputeEffectiveStatus(inv, today).ToScreamingSnakeCase(),
                    tx.AnticipatedAtUtc,
                    CanAnticipate: false);
            })
            .ToArray();

        return new AnticipationResultDto(
            groupId,
            mode.ToScreamingSnakeCase(),
            updated.Count,
            originalAmounts.Sum(),
            roundedDiscount,
            netAmounts.Sum(),
            targetInvoiceId,
            paymentAccountId,
            items);
    }

    private static bool IsStrictlyAfterInvoice(CreditCardInvoice candidate, CreditCardInvoice reference)
        => candidate.ReferenceYear > reference.ReferenceYear
           || (candidate.ReferenceYear == reference.ReferenceYear && candidate.ReferenceMonth > reference.ReferenceMonth);

    private async Task<Account> RequireAccountAsync(Guid userId, Guid accountId, CancellationToken ct)
        => await accountRepository.GetByIdAsync(userId, accountId, ct)
            ?? throw AppException.NotFound("account_not_found", "Conta não encontrada.");

    private async Task<Account> RequireActiveAccountAsync(Guid userId, Guid accountId, CancellationToken ct)
    {
        Account account = await RequireAccountAsync(userId, accountId, ct);
        if (account.IsArchived)
        {
            throw AppException.Unprocessable("account_archived", "A conta selecionada está arquivada.");
        }

        return account;
    }

    private async Task ValidateCategoryAsync(
        Guid userId,
        Guid? categoryId,
        TransactionType transactionType,
        CancellationToken ct)
    {
        if (!categoryId.HasValue)
            return;

        Category category = await categoryRepository.GetByIdAsync(userId, categoryId.Value, ct)
            ?? throw AppException.NotFound("category_not_found", "Categoria não encontrada.");

        if (category.IsArchived)
        {
            throw AppException.Unprocessable("category_archived", "A categoria selecionada está arquivada.");
        }

        CategoryType expectedType = transactionType == TransactionType.Income
            ? CategoryType.Income
            : CategoryType.Expense;

        if (category.Type != expectedType)
        {
            throw AppException.Unprocessable(
                "category_type_mismatch",
                "O tipo da categoria não é compatível com o tipo do lançamento.");
        }
    }

    private static void ApplySignedDelta(Account account, TransactionType type, decimal amount)
    {
        decimal signedDelta = type is TransactionType.Income or TransactionType.TransferIn
            ? amount
            : -amount;

        account.ApplyBalanceChange(signedDelta);
    }

    private static TransactionDto MapTransactionDto(Transaction tx)
        => new(
            tx.Id,
            tx.AccountId,
            tx.CreditCardInvoiceId,
            tx.TransferPairId,
            tx.RecurrenceRuleId,
            tx.CategoryId,
            tx.InstallmentGroupId,
            tx.Type.ToScreamingSnakeCase(),
            tx.Status.ToScreamingSnakeCase(),
            tx.Description,
            tx.Amount,
            tx.OriginalAmount,
            tx.TransactionDate,
            tx.InstallmentNumber,
            tx.TotalInstallments,
            tx.Notes,
            tx.AnticipatedAtUtc,
            tx.CreatedAtUtc);
}
