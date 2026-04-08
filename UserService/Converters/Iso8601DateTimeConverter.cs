using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UserService.Converters;

/// <summary>
/// Custom JSON converter that ensures all DateTime values are serialised in UTC
/// with a trailing Z suffix (ISO 8601). On deserialisation it converts any
/// incoming value to UTC so that the rest of the application never works with
/// ambiguous DateTimeKind.Unspecified values.
/// </summary>
public sealed class Iso8601DateTimeConverter : JsonConverter<DateTime>
{
    private const string Iso8601Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return default;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();

        writer.WriteStringValue(utc.ToString(Iso8601Format, CultureInfo.InvariantCulture));
    }
}
