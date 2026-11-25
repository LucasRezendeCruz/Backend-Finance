using Finance.Application.Common;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Common;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using FluentValidation;

namespace Finance.Application.CreditCards;

public sealed class CreditCardService(
    ICreditCardRepository creditCardRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateCreditCardCommand> createValidator,
    IValidator<UpdateCreditCardCommand> updateValidator,
    IValidator<PayInvoiceCommand> payValidator) : ICreditCardService
{
    public async Task<IReadOnlyList<CreditCardDto>> GetAllAsync(bool includeArchived, CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        IReadOnlyList<CreditCard> cards = await creditCardRepository.GetByUserAsync(userId, includeArchived, ct);
        IReadOnlyList<CreditCardInvoice> unpaidInvoices = await creditCardRepository.GetUnpaidInvoicesByUserAsync(userId, ct);

        Dictionary<Guid, decimal> usedByCard = unpaidInvoices
            .GroupBy(inv => inv.CreditCardId)
            .ToDictionary(g => g.Key, g => g.Sum(inv => inv.TotalAmount));

        return cards
            .Select(card =>
            {
                decimal used = usedByCard.GetValueOrDefault(card.Id, 0m);
                return MapCardDto(card, used);
            })
            .ToArray();
    }

    public async Task<CreditCardDto> CreateAsync(CreateCreditCardCommand command, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        if (command.DefaultAccountId.HasValue)
        {
            await RequireOwnedActiveAccountAsync(userId, command.DefaultAccountId.Value, ct);
        }

        var card = new CreditCard(
            userId,
            command.DefaultAccountId,
            command.Name,
            command.CreditLimit,
            command.ClosingDay,
            command.DueDay,
            command.Color ?? "#ff5a36");

        await creditCardRepository.AddAsync(card, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapCardDto(card, 0m);
    }

    public async Task<CreditCardDto> UpdateAsync(Guid id, UpdateCreditCardCommand command, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        CreditCard card = await creditCardRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("credit_card_not_found", "Cartão de crédito não encontrado.");

        if (command.DefaultAccountId.HasValue)
        {
            await RequireOwnedActiveAccountAsync(userId, command.DefaultAccountId.Value, ct);
        }

        card.Update(
            command.DefaultAccountId,
            command.Name,
            command.CreditLimit,
            command.ClosingDay,
            command.DueDay,
            command.Color ?? string.Empty);

        IReadOnlyList<CreditCardInvoice> openInvoices = await creditCardRepository.GetOpenInvoicesByCardAsync(card.Id, ct);
        foreach (CreditCardInvoice invoice in openInvoices)
        {
            (DateOnly closingDate, DateOnly dueDate) = card.CalculateInvoiceDates(invoice.ReferenceYear, invoice.ReferenceMonth);
            invoice.UpdateBillingDates(closingDate, dueDate);
        }

        await unitOfWork.SaveChangesAsync(ct);

        IReadOnlyList<CreditCardInvoice> unpaid = await creditCardRepository.GetUnpaidInvoicesByCardAsync(card.Id, ct);
        decimal used = unpaid.Sum(i => i.TotalAmount);
        return MapCardDto(card, used);
    }

    public async Task<IReadOnlyList<CreditCardInvoiceDto>> GetInvoicesAsync(Guid cardId, int? year, CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        _ = await creditCardRepository.GetByIdAsync(userId, cardId, ct)
            ?? throw AppException.NotFound("credit_card_not_found", "Cartão de crédito não encontrado.");

        DateOnly today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        IReadOnlyList<CreditCardInvoice> invoices = await creditCardRepository.GetInvoicesByCardAsync(userId, cardId, year, ct);

        return invoices
            .Select(inv => MapInvoiceDto(inv, today))
            .ToArray();
    }

    public async Task<CreditCardInvoiceDetailDto> GetInvoiceDetailAsync(
        Guid cardId,
        Guid invoiceId,
        CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        CreditCard card = await creditCardRepository.GetByIdAsync(userId, cardId, ct)
            ?? throw AppException.NotFound("credit_card_not_found", "Cartão de crédito não encontrado.");

        CreditCardInvoice invoice = await creditCardRepository.GetInvoiceByIdAsync(userId, cardId, invoiceId, ct)
            ?? throw AppException.NotFound("invoice_not_found", "Fatura não encontrada.");

        IReadOnlyList<Transaction> transactions = await transactionRepository.GetByInvoiceIdAsync(userId, invoice.Id, ct);
        DateOnly today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        IReadOnlyList<InvoiceTransactionItemDto> items = transactions
            .Select(tx => new InvoiceTransactionItemDto(
                tx.Id,
                tx.CategoryId,
                tx.InstallmentGroupId,
                tx.Description,
                tx.Amount,
                tx.OriginalAmount,
                tx.TransactionDate,
                tx.InstallmentNumber,
                tx.TotalInstallments,
                tx.Notes,
                tx.AnticipatedAtUtc))
            .ToArray();

        return new CreditCardInvoiceDetailDto(
            invoice.Id,
            invoice.CreditCardId,
            card.Name,
            invoice.ReferenceYear,
            invoice.ReferenceMonth,
            invoice.ClosingDate,
            invoice.DueDate,
            ComputeEffectiveStatus(invoice, today).ToScreamingSnakeCase(),
            invoice.TotalAmount,
            invoice.PaidAtUtc,
            invoice.PaidWithAccountId,
            items);
    }

    public async Task<CreditCardInvoiceDto> PayInvoiceAsync(
        Guid cardId,
        Guid invoiceId,
        PayInvoiceCommand command,
        CancellationToken ct = default)
    {
        await payValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        CreditCard card = await creditCardRepository.GetByIdAsync(userId, cardId, ct)
            ?? throw AppException.NotFound("credit_card_not_found", "Cartão de crédito não encontrado.");

        CreditCardInvoice invoice = await creditCardRepository.GetInvoiceByIdAsync(userId, cardId, invoiceId, ct)
            ?? throw AppException.NotFound("invoice_not_found", "Fatura não encontrada.");

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw AppException.Unprocessable("invoice_already_paid", "Esta fatura já foi paga.");
        }

        if (invoice.TotalAmount <= 0m)
        {
            throw AppException.Unprocessable("invoice_empty", "Não é possível pagar uma fatura com valor zerado.");
        }

        Guid paymentAccountId = command.AccountId
            ?? card.DefaultAccountId
            ?? throw AppException.Unprocessable(
                "payment_account_required",
                "Informe a conta pagadora ou configure uma conta padrão no cartão.");

        Account account = await RequireOwnedActiveAccountAsync(userId, paymentAccountId, ct);
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        DateOnly paymentDate = command.PaymentDate ?? DateOnly.FromDateTime(utcNow.UtcDateTime);

        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        var paymentTransaction = new Transaction(
            userId,
            account.Id,
            invoice.Id,
            null,
            TransactionType.InvoicePayment,
            TransactionStatus.Confirmed,
            $"Pagamento fatura {card.Name} ({invoice.ReferenceMonth:D2}/{invoice.ReferenceYear})",
            invoice.TotalAmount,
            paymentDate,
            utcNow);

        account.ApplyBalanceChange(-invoice.TotalAmount);
        invoice.MarkAsPaid(account.Id, utcNow);

        await transactionRepository.AddAsync(paymentTransaction, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);

        return MapInvoiceDto(invoice, paymentDate);
    }

    private async Task<Account> RequireOwnedActiveAccountAsync(Guid userId, Guid accountId, CancellationToken ct)
    {
        Account account = await accountRepository.GetByIdAsync(userId, accountId, ct)
            ?? throw AppException.NotFound("account_not_found", "Conta não encontrada.");

        if (account.IsArchived)
        {
            throw AppException.Unprocessable("account_archived", "A conta selecionada está arquivada.");
        }

        return account;
    }

    private static CreditCardDto MapCardDto(CreditCard card, decimal usedLimit)
        => new(
            card.Id,
            card.DefaultAccountId,
            card.Name,
            card.CreditLimit,
            usedLimit,
            card.CreditLimit - usedLimit,
            card.ClosingDay,
            card.DueDay,
            card.Color,
            card.IsArchived);

    private static CreditCardInvoiceDto MapInvoiceDto(CreditCardInvoice invoice, DateOnly today)
        => new(
            invoice.Id,
            invoice.CreditCardId,
            invoice.ReferenceYear,
            invoice.ReferenceMonth,
            invoice.ClosingDate,
            invoice.DueDate,
            ComputeEffectiveStatus(invoice, today).ToScreamingSnakeCase(),
            invoice.TotalAmount,
            invoice.PaidAtUtc,
            invoice.PaidWithAccountId);

    internal static InvoiceStatus ComputeEffectiveStatus(CreditCardInvoice invoice, DateOnly today)
        => invoice.Status == InvoiceStatus.Open && today >= invoice.ClosingDate
            ? InvoiceStatus.Closed
            : invoice.Status;
}
