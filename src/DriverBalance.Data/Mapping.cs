using System.Text.Json;
using DriverBalance.Core;

namespace DriverBalance.Data;

public static class Mapping
{
    public static Trip ToDomain(this TripRow r) => new(
        r.Category, r.Start, r.StartOdometer, r.StartAddress, r.End, r.EndOdometer, r.EndAddress,
        TimeSpan.FromTicks(r.DurationTicks), r.DistanceKm, r.FuelLiters, r.Notes)
    { Id = r.Id };

    /// <summary>Maps to a new row; the caller sets VehicleId and ImportBatchId, UserId is stamped on save.</summary>
    public static TripRow ToRow(this Trip t) => new()
    {
        Id = t.Id, Category = t.Category, Start = t.Start, StartOdometer = t.StartOdometer, StartAddress = t.StartAddress,
        End = t.End, EndOdometer = t.EndOdometer, EndAddress = t.EndAddress, DurationTicks = t.Duration.Ticks,
        DistanceKm = t.DistanceKm, FuelLiters = t.FuelLiters, Notes = t.Notes,
    };

    public static Expense ToDomain(this ExpenseRow r) => new()
    {
        Id = r.Id, Date = r.Date, Category = r.Category, Amount = r.Amount, Currency = r.Currency, HomeAmount = r.HomeAmount,
        Odometer = r.Odometer, Note = r.Note, CoversFrom = r.CoversFrom, CoversTo = r.CoversTo, Litres = r.Litres,
        FullTank = r.FullTank, Station = r.Station, Maintenance = (MaintenanceType)r.MaintenanceFlags,
        MaintenanceDescription = r.MaintenanceDescription, Work = WorkFromJson(r.WorkJson), TyreSetId = r.TyreSetId, Source = r.Source,
    };

    private static IReadOnlyList<MaintenanceWork> WorkFromJson(string? json) =>
        string.IsNullOrEmpty(json) ? Array.Empty<MaintenanceWork>() : JsonSerializer.Deserialize<List<MaintenanceWork>>(json) ?? (IReadOnlyList<MaintenanceWork>)Array.Empty<MaintenanceWork>();

    private static string? WorkToJson(IReadOnlyList<MaintenanceWork>? work) =>
        work is null or { Count: 0 } ? null : JsonSerializer.Serialize(work);

    public static ExpenseRow ToRow(this Expense e) => new()
    {
        Id = e.Id, Date = e.Date, Category = e.Category, Amount = e.Amount, Currency = e.Currency, HomeAmount = e.HomeAmount,
        Odometer = e.Odometer, Note = e.Note, CoversFrom = e.CoversFrom, CoversTo = e.CoversTo, Litres = e.Litres,
        FullTank = e.FullTank, Station = e.Station, MaintenanceFlags = (int)e.Maintenance,
        MaintenanceDescription = e.MaintenanceDescription, WorkJson = WorkToJson(e.Work), TyreSetId = e.TyreSetId, Source = e.Source,
        CreatedAt = DateTime.UtcNow,
    };

    public static TyreSet ToDomain(this TyreSetRow r) => new()
    {
        Id = r.Id, Name = r.Name, Season = r.Season, Description = r.Description, PurchaseDate = r.PurchaseDate, Retired = r.Retired,
    };

    public static TyreSetRow ToRow(this TyreSet t) => new()
    {
        Id = t.Id, Name = t.Name, Season = t.Season, Description = t.Description, PurchaseDate = t.PurchaseDate, Retired = t.Retired,
    };

    private static readonly JsonSerializerOptions ParamOptions = new(JsonSerializerDefaults.Web);

    public static string ToJson(this AnalysisParameters p) => JsonSerializer.Serialize(p, ParamOptions);

    /// <summary>Missing properties keep their defaults, so older stored JSON stays readable as parameters grow.</summary>
    public static AnalysisParameters ParseParameters(string json) =>
        JsonSerializer.Deserialize<AnalysisParameters>(string.IsNullOrWhiteSpace(json) ? "{}" : json, ParamOptions) ?? new AnalysisParameters();
}
