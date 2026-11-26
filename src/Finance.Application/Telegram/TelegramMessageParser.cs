using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Telegram;

public enum TelegramCommandKind
{
    Unknown,
    Help,
    Link,
    Balance,
    Summary,
    Budgets,
    Undo,
    Unlink,
    QuickTransaction
}

public sealed record ParsedTelegramInput(
    TelegramCommandKind Kind,
    string? LinkToken = null,
    TransactionType? TransactionType = null,
    string? Description = null,
    decimal? Amount = null,
    int Installments = 1,
    bool UseCreditCard = false,
    IReadOnlyList<string>? RawTokens = null,
    int AmountTokenIndex = -1);

public sealed record ResolvedTelegramSource(
    Guid? AccountId,
    Guid? CreditCardId,
    string? SourceName,
    string CleanDescription,
    string? PaymentMethodTag);

public static partial class TelegramMessageParser
{
    [GeneratedRegex(@"^(\d{1,3})x$", RegexOptions.IgnoreCase)]
    private static partial Regex InstallmentsRegex();

    private static readonly HashSet<string> CardKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "cartao", "cartão", "credito", "crédito", "cc", "card", "fatura"
    };

    private static readonly Dictionary<string, string> AccountMethodKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pix"] = "PIX",
        ["debito"] = "Débito",
        ["dinheiro"] = "Dinheiro",
        ["especie"] = "Dinheiro",
        ["ted"] = "TED",
        ["doc"] = "DOC",
        ["transferencia"] = "Transferência"
    };

    private static readonly HashSet<string> SourceCueWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "no", "na", "nos", "nas", "do", "da", "dos", "das",
        "pelo", "pela", "via", "com", "em", "por", "para", "pra",
        "conta", "banco", "cartao", "credito", "debito", "pix", "dinheiro", "especie", "ted", "doc", "transferencia", "fatura"
    };

    private static readonly HashSet<string> PrepositionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "no", "na", "nos", "nas", "do", "da", "dos", "das",
        "pelo", "pela", "via", "com", "em", "por", "para", "pra", "de"
    };

    private static readonly HashSet<string> GenericSourceWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "conta", "banco", "cartao", "credito", "debito", "corrente", "poupanca", "digital", "principal", "pessoal", "card"
    };

    private static readonly (string CategoryHint, string[] Keywords)[] CategoryKeywordMap =
    [
        ("aliment", ["mercado", "supermercado", "ifood", "restaurante", "cafe", "café", "padaria", "almoco", "almoço", "jantar", "lanche", "pizza", "burger", "acai", "açaí", "feira"]),
        ("transp", ["uber", "99", "taxi", "táxi", "gasolina", "combustivel", "combustível", "estacionamento", "pedagio", "pedágio", "metro", "metrô", "onibus", "ônibus"]),
        ("morad", ["aluguel", "condominio", "condomínio", "energia", "luz", "agua", "água", "gas", "gás", "iptu"]),
        ("assinat", ["internet", "fibra", "netflix", "spotify", "aws", "icloud", "youtube", "chatgpt", "github", "prime", "disney", "hbo"]),
        ("saud", ["farmacia", "farmácia", "remedio", "remédio", "medico", "médico", "dentista", "consulta", "exame", "academia", "plano"]),
        ("educa", ["curso", "livro", "faculdade", "escola", "udemy", "alura"]),
        ("lazer", ["cinema", "show", "bar", "viagem", "hotel", "jogo", "steam", "ingresso"]),
        ("tecnolog", ["notebook", "macbook", "monitor", "teclado", "mouse", "iphone", "celular", "fone", "ssd"]),
        ("salar", ["salario", "salário", "pagamento", "holerite", "adiantamento"]),
        ("freel", ["freelance", "freela", "consultoria", "projeto", "bico"]),
        ("invest", ["dividendo", "dividendos", "rendimento", "juros", "cdb", "fii"])
    ];

    public static ParsedTelegramInput Parse(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new ParsedTelegramInput(TelegramCommandKind.Unknown);

        string text = rawText.Trim();
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return new ParsedTelegramInput(TelegramCommandKind.Unknown);

        string first = NormalizeCommand(parts[0]);

        switch (first)
        {
            case "/start":
                if (parts.Length >= 2)
                    return new ParsedTelegramInput(TelegramCommandKind.Link, LinkToken: parts[1].Trim());
                return new ParsedTelegramInput(TelegramCommandKind.Help);

            case "/vincular":
                return parts.Length >= 2
                    ? new ParsedTelegramInput(TelegramCommandKind.Link, LinkToken: parts[1].Trim())
                    : new ParsedTelegramInput(TelegramCommandKind.Link);

            case "/saldo":
                return new ParsedTelegramInput(TelegramCommandKind.Balance);

            case "/resumo":
                return new ParsedTelegramInput(TelegramCommandKind.Summary);

            case "/orcamentos":
            case "/orçamentos":
                return new ParsedTelegramInput(TelegramCommandKind.Budgets);

            case "/desfazer":
                return new ParsedTelegramInput(TelegramCommandKind.Undo);

            case "/desconectar":
                return new ParsedTelegramInput(TelegramCommandKind.Unlink);

            case "/ajuda":
            case "/help":
                return new ParsedTelegramInput(TelegramCommandKind.Help);

            case "/receita":
                return ParseQuickTransaction(parts.Skip(1).ToArray(), TransactionType.Income);

            case "/gasto":
            case "/despesa":
                return ParseQuickTransaction(parts.Skip(1).ToArray(), TransactionType.Expense);
        }

        if (text.StartsWith('+'))
        {
            string rest = text[1..].Trim();
            string[] tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return ParseQuickTransaction(tokens, TransactionType.Income);
        }

        if (first.StartsWith('/'))
            return new ParsedTelegramInput(TelegramCommandKind.Unknown);

        return ParseQuickTransaction(parts, TransactionType.Expense);
    }

    public static Category? MatchCategory(string description, CategoryType type, IReadOnlyList<Category> categories)
    {
        List<Category> eligible = categories
            .Where(c => !c.IsArchived && c.Type == type)
            .ToList();

        if (eligible.Count == 0)
            return null;

        string normalizedDesc = RemoveDiacritics(description).ToLowerInvariant();

        // 1. Direct category name match inside description
        foreach (Category cat in eligible)
        {
            string normalizedCat = RemoveDiacritics(cat.Name).ToLowerInvariant();
            if (normalizedCat.Length >= 3 && normalizedDesc.Contains(normalizedCat, StringComparison.Ordinal))
                return cat;
        }

        // 2. Keyword catalog match
        foreach ((string hint, string[] keywords) in CategoryKeywordMap)
        {
            if (keywords.Any(kw => normalizedDesc.Contains(RemoveDiacritics(kw).ToLowerInvariant(), StringComparison.Ordinal)))
            {
                Category? match = eligible.FirstOrDefault(c =>
                    RemoveDiacritics(c.Name).ToLowerInvariant().Contains(hint, StringComparison.Ordinal));
                if (match is not null)
                    return match;
            }
        }

        // 3. Default fallback ("Outros" / "Outras Receitas" / first eligible)
        return eligible.FirstOrDefault(c =>
            RemoveDiacritics(c.Name).ToLowerInvariant().Contains("outr", StringComparison.Ordinal))
            ?? eligible[0];
    }

    public static ResolvedTelegramSource ResolvePaymentSource(
        ParsedTelegramInput parsed,
        IReadOnlyList<Account> accounts,
        IReadOnlyList<CreditCard> cards,
        Guid? defaultAccountId,
        Guid? defaultCardId)
    {
        IReadOnlyList<string> rawTokens = parsed.RawTokens ?? [];
        int amountIdx = parsed.AmountTokenIndex;
        bool isIncome = parsed.TransactionType == TransactionType.Income;

        // Collect all non-amount / non-card / non-installment tokens with their original indices
        var descTokens = new List<(int OrigIdx, string Raw, string Norm)>();
        string? paymentMethodTag = null;
        bool hasAccountMethodHint = false;
        bool hasContaAfterAmount = false;

        for (int i = 0; i < rawTokens.Count; i++)
        {
            if (i == amountIdx)
                continue;

            string raw = rawTokens[i].Trim();
            if (raw.Equals("r$", StringComparison.OrdinalIgnoreCase))
                continue;

            string norm = NormalizeWord(raw);
            if (CardKeywords.Contains(raw) || CardKeywords.Contains(norm))
                continue;

            if (InstallmentsRegex().IsMatch(raw))
                continue;

            if (AccountMethodKeywords.TryGetValue(norm, out string? tag))
            {
                hasAccountMethodHint = true;
                paymentMethodTag ??= tag;
            }

            if (norm == "conta" && (i > amountIdx || (i > 0 && SourceCueWords.Contains(NormalizeWord(rawTokens[i - 1])))))
            {
                hasContaAfterAmount = true;
            }

            descTokens.Add((i, raw, norm));
        }

        bool hasAccountHint = !parsed.UseCreditCard && (hasAccountMethodHint || hasContaAfterAmount);

        Account? matchedAccount = null;
        CreditCard? matchedCard = null;
        HashSet<int> matchedDescIndices = [];
        int bestScore = 0;

        // Evaluate accounts
        if (parsed.Installments <= 1)
        {
            foreach (Account acc in accounts)
            {
                if (acc.IsArchived)
                    continue;

                if (TryScoreCandidate(
                    acc.Name,
                    isCard: false,
                    parsed,
                    hasAccountHint,
                    hasAccountMethodHint,
                    rawTokens,
                    amountIdx,
                    descTokens,
                    out int score,
                    out HashSet<int> matchedIndices) && score > bestScore)
                {
                    bestScore = score;
                    matchedAccount = acc;
                    matchedCard = null;
                    matchedDescIndices = matchedIndices;
                }
            }
        }

        // Evaluate credit cards (expenses only)
        if (!isIncome)
        {
            foreach (CreditCard card in cards)
            {
                if (card.IsArchived)
                    continue;

                if (TryScoreCandidate(
                    card.Name,
                    isCard: true,
                    parsed,
                    hasAccountHint,
                    hasAccountMethodHint,
                    rawTokens,
                    amountIdx,
                    descTokens,
                    out int score,
                    out HashSet<int> matchedIndices) && score > bestScore)
                {
                    bestScore = score;
                    matchedCard = card;
                    matchedAccount = null;
                    matchedDescIndices = matchedIndices;
                }
            }
        }

        // Determine which description tokens to strip (matched source + adjacent filler/payment cues)
        var removeIndices = new HashSet<int>(matchedDescIndices);

        // Strip generic "conta"/"banco" right next to a matched account/card token
        if (matchedDescIndices.Count > 0)
        {
            for (int d = 0; d < descTokens.Count; d++)
            {
                if (removeIndices.Contains(d))
                    continue;

                string norm = descTokens[d].Norm;
                if ((norm is "conta" or "banco") && (removeIndices.Contains(d - 1) || removeIndices.Contains(d + 1)))
                {
                    removeIndices.Add(d);
                }
            }
        }

        // Strip payment-method words (pix, débito, etc.) if after amount or adjacent to removed tokens
        for (int d = 0; d < descTokens.Count; d++)
        {
            if (removeIndices.Contains(d))
                continue;

            if (AccountMethodKeywords.ContainsKey(descTokens[d].Norm) &&
                (descTokens[d].OrigIdx > amountIdx || removeIndices.Contains(d - 1) || removeIndices.Contains(d + 1)))
            {
                removeIndices.Add(d);
            }
        }

        // Strip prepositions directly preceding a removed token (e.g., "no", "na", "pelo", "via", "com")
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int d = descTokens.Count - 1; d >= 0; d--)
            {
                if (removeIndices.Contains(d))
                    continue;

                if (PrepositionWords.Contains(descTokens[d].Norm) && removeIndices.Contains(d + 1))
                {
                    removeIndices.Add(d);
                    changed = true;
                }
            }
        }

        List<string> kept = [];
        for (int d = 0; d < descTokens.Count; d++)
        {
            if (!removeIndices.Contains(d))
                kept.Add(descTokens[d].Raw);
        }

        string cleanDescription = kept.Count > 0
            ? string.Join(' ', kept)
            : parsed.Description ?? (isIncome ? "Receita via Telegram" : "Gasto via Telegram");

        // Resolve final target account or card
        if (matchedCard is not null)
        {
            return new ResolvedTelegramSource(null, matchedCard.Id, matchedCard.Name, cleanDescription, paymentMethodTag);
        }

        if (matchedAccount is not null)
        {
            return new ResolvedTelegramSource(matchedAccount.Id, null, matchedAccount.Name, cleanDescription, paymentMethodTag);
        }

        if (parsed.UseCreditCard && !isIncome && cards.Count > 0)
        {
            CreditCard chosenCard = cards.FirstOrDefault(c => c.Id == defaultCardId) ?? cards[0];
            return new ResolvedTelegramSource(null, chosenCard.Id, chosenCard.Name, cleanDescription, paymentMethodTag);
        }

        Account? chosenAccount = accounts.FirstOrDefault(a => a.Id == defaultAccountId) ?? accounts.FirstOrDefault();
        return new ResolvedTelegramSource(chosenAccount?.Id, null, chosenAccount?.Name, cleanDescription, paymentMethodTag);
    }

    private static bool TryScoreCandidate(
        string candidateName,
        bool isCard,
        ParsedTelegramInput parsed,
        bool hasAccountHint,
        bool hasAccountMethodHint,
        IReadOnlyList<string> rawTokens,
        int amountIdx,
        List<(int OrigIdx, string Raw, string Norm)> descTokens,
        out int score,
        out HashSet<int> matchedDescIndices)
    {
        score = 0;
        matchedDescIndices = [];

        string[] nameTokens = candidateName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeWord)
            .Where(t => t.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (nameTokens.Length == 0)
            return false;

        string[] distinctiveTokens = nameTokens
            .Where(t => !GenericSourceWords.Contains(t))
            .ToArray();

        var matchedNameTokens = new HashSet<string>(StringComparer.Ordinal);
        int distinctiveMatchCount = 0;
        bool anyContextValid = false;
        bool anyAfterAmount = false;
        bool anyPrecededByCue = false;

        for (int d = 0; d < descTokens.Count; d++)
        {
            (int origIdx, _, string norm) = descTokens[d];
            if (!nameTokens.Contains(norm, StringComparer.Ordinal))
                continue;

            bool isDistinctive = distinctiveTokens.Contains(norm, StringComparer.Ordinal);
            bool afterAmount = amountIdx >= 0 && origIdx > amountIdx;
            bool precededByCue = origIdx > 0 && SourceCueWords.Contains(NormalizeWord(rawTokens[origIdx - 1]));
            bool followedByPossessiveDe = origIdx + 1 < rawTokens.Count &&
                NormalizeWord(rawTokens[origIdx + 1]) is "de" or "da" or "do" or "dos" or "das";

            // Avoid matching product nouns like "Carteira de couro 120" or "Conta de luz 180"
            if (!afterAmount && !precededByCue && followedByPossessiveDe)
                continue;

            matchedDescIndices.Add(d);
            if (matchedNameTokens.Add(norm) && isDistinctive)
            {
                distinctiveMatchCount++;
            }

            if (isDistinctive || distinctiveTokens.Length == 0)
            {
                if (afterAmount) anyAfterAmount = true;
                if (precededByCue) anyPrecededByCue = true;
                anyContextValid = true;
            }
        }

        if (!anyContextValid || matchedDescIndices.Count == 0)
            return false;

        if (distinctiveTokens.Length > 0 && distinctiveMatchCount == 0)
            return false;

        if (distinctiveTokens.Length == 0 && matchedNameTokens.Count < nameTokens.Length)
            return false;

        // Ensure stripping this source wouldn't wipe the entire description unless a payment cue exists
        int remainingNonFiller = 0;
        for (int d = 0; d < descTokens.Count; d++)
        {
            if (matchedDescIndices.Contains(d))
                continue;
            string n = descTokens[d].Norm;
            if (!PrepositionWords.Contains(n) && !AccountMethodKeywords.ContainsKey(n) && n is not ("conta" or "banco"))
                remainingNonFiller++;
        }

        if (remainingNonFiller == 0 && !hasAccountMethodHint && !parsed.UseCreditCard)
            return false;

        int hintBias = 0;
        if (parsed.UseCreditCard)
        {
            hintBias = isCard ? 1000 : -600;
        }
        else if (hasAccountHint)
        {
            hintBias = isCard ? -600 : 1000;
        }

        double coverage = (double)matchedNameTokens.Count / nameTokens.Length;
        score = hintBias
            + (distinctiveMatchCount * 120)
            + (matchedNameTokens.Count * 40)
            + (int)(coverage * 80)
            + (anyAfterAmount ? 35 : 0)
            + (anyPrecededByCue ? 25 : 0);

        return score > 0;
    }

    private static string NormalizeWord(string raw)
    {
        string trimmed = raw.Trim().Trim(',', '.', ';', ':', '!', '?', '(', ')', '-', '"', '\'');
        return RemoveDiacritics(trimmed).ToLowerInvariant();
    }

    private static ParsedTelegramInput ParseQuickTransaction(string[] tokens, TransactionType txType)
    {
        if (tokens.Length == 0)
            return new ParsedTelegramInput(TelegramCommandKind.Unknown);

        decimal? amount = null;
        int amountTokenIndex = -1;
        int installments = 1;
        bool useCreditCard = false;
        var descriptionParts = new List<string>();

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim();
            if (token.Equals("r$", StringComparison.OrdinalIgnoreCase))
                continue;

            if (CardKeywords.Contains(token) || CardKeywords.Contains(NormalizeWord(token)))
            {
                useCreditCard = true;
                continue;
            }

            Match instMatch = InstallmentsRegex().Match(token);
            if (instMatch.Success && int.TryParse(instMatch.Groups[1].Value, out int parsedInst) && parsedInst is >= 1 and <= 120)
            {
                installments = parsedInst;
                if (parsedInst > 1)
                    useCreditCard = true;
                continue;
            }

            if (amount is null && TryParseAmount(token, out decimal parsedAmount))
            {
                amount = parsedAmount;
                amountTokenIndex = i;
                continue;
            }

            descriptionParts.Add(token);
        }

        if (amount is null || amount.Value <= 0m)
            return new ParsedTelegramInput(TelegramCommandKind.Unknown);

        string description = descriptionParts.Count > 0
            ? string.Join(' ', descriptionParts)
            : txType == TransactionType.Income ? "Receita via Telegram" : "Gasto via Telegram";

        if (txType == TransactionType.Income)
        {
            useCreditCard = false;
            installments = 1;
        }

        return new ParsedTelegramInput(
            TelegramCommandKind.QuickTransaction,
            TransactionType: txType,
            Description: description,
            Amount: amount.Value,
            Installments: installments,
            UseCreditCard: useCreditCard,
            RawTokens: tokens,
            AmountTokenIndex: amountTokenIndex);
    }

    private static bool TryParseAmount(string rawToken, out decimal value)
    {
        value = 0m;
        string cleaned = rawToken.Trim();
        if (cleaned.StartsWith("R$", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[2..].Trim();

        if (cleaned.Length == 0 || !cleaned.Any(char.IsDigit))
            return false;

        // Only digits, dots, commas allowed
        if (cleaned.Any(ch => !char.IsDigit(ch) && ch != '.' && ch != ','))
            return false;

        string normalized;
        if (cleaned.Contains(','))
        {
            normalized = cleaned.Replace(".", string.Empty, StringComparison.Ordinal)
                .Replace(',', '.');
        }
        else
        {
            int dotCount = cleaned.Count(ch => ch == '.');
            if (dotCount > 1)
            {
                normalized = cleaned.Replace(".", string.Empty, StringComparison.Ordinal);
            }
            else if (dotCount == 1)
            {
                int digitsAfterDot = cleaned.Length - cleaned.IndexOf('.') - 1;
                normalized = digitsAfterDot == 3
                    ? cleaned.Replace(".", string.Empty, StringComparison.Ordinal)
                    : cleaned;
            }
            else
            {
                normalized = cleaned;
            }
        }

        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
            return false;

        if (parsed <= 0m)
            return false;

        value = decimal.Round(parsed, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    private static string NormalizeCommand(string raw)
    {
        int atIdx = raw.IndexOf('@');
        string clean = atIdx > 0 ? raw[..atIdx] : raw;
        return clean.ToLowerInvariant();
    }

    private static string RemoveDiacritics(string text)
    {
        string normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
