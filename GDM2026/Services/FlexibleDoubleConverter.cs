using System;
using System.Globalization;
using Newtonsoft.Json;

namespace GDM2026.Services;

public sealed class FlexibleDoubleConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        var targetType = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return targetType == typeof(double);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return Nullable.GetUnderlyingType(objectType) != null ? null : 0d;
        }

        if (reader.TokenType is JsonToken.Float or JsonToken.Integer)
        {
            return Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonToken.String)
        {
            var raw = (reader.Value as string)?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Nullable.GetUnderlyingType(objectType) != null ? null : 0d;
            }

            var styles = NumberStyles.Number;
            if (double.TryParse(raw, styles, CultureInfo.CurrentCulture, out var current))
            {
                return current;
            }

            if (double.TryParse(raw, styles, CultureInfo.InvariantCulture, out var invariant))
            {
                return invariant;
            }

            var normalized = raw.Replace(',', '.');
            if (double.TryParse(normalized, styles, CultureInfo.InvariantCulture, out var normalizedValue))
            {
                return normalizedValue;
            }
        }

        throw new JsonSerializationException($"Valeur numérique invalide: '{reader.Value}'.");
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
    }
}
