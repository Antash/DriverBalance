using System.Text.Json;
using System.Text.Json.Serialization;
using DriverBalance.Core;

namespace DriverBalance.Web;

/// <summary>MaintenanceType flags as a JSON array of flag names; Custom (0) is the empty array.</summary>
public sealed class MaintenanceTypeJsonConverter : JsonConverter<MaintenanceType>
{
    private static readonly Dictionary<string, MaintenanceType> ByName =
        Enum.GetValues<MaintenanceType>().ToDictionary(v => v.ToString(), v => v, StringComparer.OrdinalIgnoreCase);

    private static readonly MaintenanceType[] SingleFlags =
        Enum.GetValues<MaintenanceType>().Where(v => v != MaintenanceType.Custom).ToArray();

    public override MaintenanceType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return Parse(reader.GetString());
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Maintenance must be an array of maintenance type names.");

        var result = MaintenanceType.Custom;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Maintenance entries must be strings.");
            result |= Parse(reader.GetString());
        }
        return result;
    }

    private static MaintenanceType Parse(string? name) =>
        name is not null && ByName.TryGetValue(name.Trim(), out var v) ? v : throw new JsonException($"Unknown maintenance type '{name}'.");

    public override void Write(Utf8JsonWriter writer, MaintenanceType value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var flag in SingleFlags)
            if ((value & flag) == flag) writer.WriteStringValue(flag.ToString());
        writer.WriteEndArray();
    }
}
