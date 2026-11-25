using Finance.Domain.Common;
using FluentValidation;
using FluentValidation.Results;

namespace Finance.Api.Controllers;

internal static class ControllerQueryHelper
{
    public static TEnum? ParseOptionalEnum<TEnum>(string? rawValue, string parameterName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        if (EnumFormattingExtensions.TryParseFlexible(rawValue, out TEnum parsed))
            return parsed;

        throw new ValidationException([
            new ValidationFailure(parameterName, $"Valor '{rawValue}' inválido para o filtro '{parameterName}'.")
        ]);
    }
}

public sealed record ArchiveRequest(bool IsArchived = true);
