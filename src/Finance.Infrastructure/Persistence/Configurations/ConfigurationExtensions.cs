using Finance.Domain.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Finance.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public static PropertyBuilder<TEnum> HasScreamingSnakeCaseConversion<TEnum>(
        this PropertyBuilder<TEnum> builder,
        int maxLength = 40)
        where TEnum : struct, Enum
    {
        var converter = new ValueConverter<TEnum, string>(
            v => v.ToScreamingSnakeCase(),
            v => EnumFormattingExtensions.ParseScreamingSnakeCase<TEnum>(v));

        return builder
            .HasConversion(converter)
            .HasMaxLength(maxLength);
    }
}
