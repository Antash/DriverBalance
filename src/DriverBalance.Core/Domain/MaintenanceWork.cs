using System.Text.Json;
using System.Text.Json.Serialization;

namespace DriverBalance.Core;

public enum PartSource { None, Original, Aftermarket }

/// <summary>Which axle or end of the car: brake pads, brake discs and suspension only.</summary>
public enum WorkPosition { Front, Rear, Both }

/// <summary>One piece of work done at a maintenance visit. Type is null for a free-text (custom) item named by Name.</summary>
public sealed record MaintenanceWork
{
    // Property-level converter so Type is a single name, not the options-level flags-as-array converter.
    [JsonConverter(typeof(WorkTypeConverter))]
    public MaintenanceType? Type { get; init; }
    public string? Name { get; init; }
    public PartSource Part { get; init; }
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public string? Details { get; init; }
    /// <summary>Front, rear or both: brake pads, brake discs and suspension only; null otherwise.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<WorkPosition>))]
    public WorkPosition? Position { get; init; }
    /// <summary>Wiper blades only: just the rubber refill was changed, not the whole blade.</summary>
    public bool RubberOnly { get; init; }
}

/// <summary>
/// A single maintenance type name or null. An unknown name reads as <see cref="UnknownType"/> so the validation rules can
/// answer 400 {errors} instead of the framework's bare binding failure.
/// </summary>
public sealed class WorkTypeConverter : JsonConverter<MaintenanceType?>
{
    public const MaintenanceType UnknownType = (MaintenanceType)(-1);

    public override MaintenanceType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.String) { reader.Skip(); return UnknownType; }
        return Enum.TryParse<MaintenanceType>(reader.GetString()?.Trim(), ignoreCase: true, out var t)
            && Enum.IsDefined(t) && !int.TryParse(reader.GetString()?.Trim(), out _) ? t : UnknownType;
    }

    public override void Write(Utf8JsonWriter writer, MaintenanceType? value, JsonSerializerOptions options)
    {
        if (value is { } v) writer.WriteStringValue(v.ToString()); else writer.WriteNullValue();
    }
}
