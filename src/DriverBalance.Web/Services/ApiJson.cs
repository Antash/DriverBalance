using System.Text.Json;
using System.Text.Json.Serialization;

namespace DriverBalance.Web;

/// <summary>The one JSON configuration: camelCase, enums as strings, MaintenanceType as a string array, dates as yyyy-MM-dd.</summary>
public static class ApiJson
{
    public static void Configure(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.PropertyNameCaseInsensitive = true;
        o.Converters.Insert(0, new MaintenanceTypeJsonConverter());
        o.Converters.Add(new JsonStringEnumConverter());
    }

    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(o);
        return o;
    }
}
