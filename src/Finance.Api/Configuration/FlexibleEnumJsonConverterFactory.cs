using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Domain.Common;
using Finance.Domain.Enums;

namespace Finance.Api.Configuration;

public sealed class FlexibleEnumJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsEnum && typeToConvert != typeof(ClientType);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type converterType = typeof(FlexibleEnumJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }

    private sealed class FlexibleEnumJsonConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Valor inválido para {typeof(TEnum).Name}. Informe uma string válida.");
            }

            string? raw = reader.GetString();
            if (!EnumFormattingExtensions.TryParseFlexible(raw, out TEnum parsed))
            {
                throw new JsonException($"Valor '{raw}' inválido para {typeof(TEnum).Name}.");
            }

            return parsed;
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToScreamingSnakeCase());
    }
}
