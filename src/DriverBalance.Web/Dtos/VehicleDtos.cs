using DriverBalance.Data;

namespace DriverBalance.Web;

public sealed record VehicleDto(long Id, string Name, string HomeCurrency, string TimeZone, long? InitialTyreSetId, bool IsDefault,
    DateOnly? HistoryStart, int? HistoryStartOdometer,
    DateOnly? PurchaseDate, decimal? EstimatedResaleValue, DateOnly? ResaleEstimateDate)
{
    public static VehicleDto From(VehicleRow r) => new(r.Id, r.Name, r.HomeCurrency, r.TimeZone, r.InitialTyreSetId, r.IsDefault, r.HistoryStart, r.HistoryStartOdometer,
        r.PurchaseDate, r.EstimatedResaleValue, r.ResaleEstimateDate);
}

public sealed record VehicleInput(string? Name, string? HomeCurrency, string? TimeZone, long? InitialTyreSetId, bool? IsDefault,
    DateOnly? HistoryStart = null, int? HistoryStartOdometer = null,
    DateOnly? PurchaseDate = null, decimal? EstimatedResaleValue = null, DateOnly? ResaleEstimateDate = null);
