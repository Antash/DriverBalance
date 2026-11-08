using System.Text.Json;

namespace DriverBalance.Core;

/// <summary>Reads the JSON files written by the legacy console app (triprecords.json, maintenancerecords.json).</summary>
public static class LegacyJsonReader
{
    private sealed class LegacyTrip
    {
        public string? Category { get; set; }
        public DateTime StartTime { get; set; }
        public int StartOdometer { get; set; }
        public string? StartAddress { get; set; }
        public DateTime EndTime { get; set; }
        public int EndOdometer { get; set; }
        public string? EndAddress { get; set; }
        public TimeSpan Duration { get; set; }
        public double DistanceKm { get; set; }
        public double FuelLiters { get; set; }
        public string? UserNotes { get; set; }
    }

    private sealed class LegacyMaintenance
    {
        public DateTime Date { get; set; }
        public int Type { get; set; }
        public string? Description { get; set; }
        public int CurrentOdometer { get; set; }
        public decimal Cost { get; set; }
        public string? Currency { get; set; }
    }

    public static IReadOnlyList<Trip> ReadTrips(Stream s) =>
        Read<LegacyTrip>(s).Select(t => new Trip(
            t.Category ?? "", t.StartTime, t.StartOdometer, t.StartAddress ?? "",
            t.EndTime, t.EndOdometer, t.EndAddress ?? "",
            t.Duration, t.DistanceKm, t.FuelLiters, t.UserNotes ?? "")).ToList();

    public static IReadOnlyList<Expense> ReadMaintenance(Stream s) =>
        Read<LegacyMaintenance>(s).Select(m => new Expense
        {
            Date = DateOnly.FromDateTime(m.Date),
            Category = ExpenseCategory.Maintenance,
            Maintenance = (MaintenanceType)m.Type,
            MaintenanceDescription = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description,
            Odometer = m.CurrentOdometer == 0 ? null : m.CurrentOdometer,
            Amount = m.Cost,
            Currency = string.IsNullOrWhiteSpace(m.Currency) ? "CZK" : m.Currency,
            Source = ExpenseSource.Migration,
        }).ToList();

    private static List<T> Read<T>(Stream s) =>
        JsonSerializer.Deserialize<List<T>>(s) ?? new List<T>();
}
