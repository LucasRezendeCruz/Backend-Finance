using System.Text;

namespace Finance.Domain.Common;

public static class EnumFormattingExtensions
{
    public static string ToScreamingSnakeCase<TEnum>(this TEnum value)
        where TEnum : struct, Enum
        => ToScreamingSnakeCase(value.ToString());

    public static string ToScreamingSnakeCase(string input)
    {
        var sb = new StringBuilder(input.Length + 6);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (i > 0 && char.IsUpper(c))
                sb.Append('_');

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }

    public static TEnum ParseScreamingSnakeCase<TEnum>(string input)
        where TEnum : struct, Enum
        => Enum.Parse<TEnum>(input.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true);

    public static bool TryParseFlexible<TEnum>(string? input, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string trimmed = input.Trim();
        if (int.TryParse(trimmed, out _))
            return false;

        string normalized = trimmed
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return Enum.TryParse(normalized, ignoreCase: true, out value) && Enum.IsDefined(value);
    }
}
