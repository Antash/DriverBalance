using DriverBalance.Data;

namespace DriverBalance.Web;

public sealed record VehicleDto(long Id, string Name, string HomeCurrency, string TimeZone, long? InitialTyreSetId, bool IsDefault)
{
    public static VehicleDto From(VehicleRow r) => new(r.Id, r.Name, r.HomeCurrency, r.TimeZone, r.InitialTyreSetId, r.IsDefault);
}

public sealed record VehicleInput(string? Name, string? HomeCurrency, string? TimeZone, long? InitialTyreSetId, bool? IsDefault);
