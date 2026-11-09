using DriverBalance.Core;

namespace DriverBalance.Data;

public interface IUserOwned
{
    long UserId { get; set; }
}

public sealed class UserRow
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class VehicleRow : IUserOwned
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public string? Vin { get; set; }
    public string FuelType { get; set; } = "";
    public string HomeCurrency { get; set; } = "CZK";
    public string TimeZone { get; set; } = "Europe/Prague";
    public string ParametersJson { get; set; } = "{}";
    public long? InitialTyreSetId { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class TripRow : IUserOwned
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long VehicleId { get; set; }
    public long? ImportBatchId { get; set; }
    public string Category { get; set; } = "";
    public DateTime Start { get; set; }
    public int StartOdometer { get; set; }
    public string StartAddress { get; set; } = "";
    public DateTime End { get; set; }
    public int EndOdometer { get; set; }
    public string EndAddress { get; set; } = "";
    public long DurationTicks { get; set; }
    public double DistanceKm { get; set; }
    public double FuelLiters { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class ExpenseRow : IUserOwned
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long VehicleId { get; set; }
    public DateOnly Date { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CZK";
    public decimal? HomeAmount { get; set; }
    public int? Odometer { get; set; }
    public string? Note { get; set; }
    public DateOnly? CoversFrom { get; set; }
    public DateOnly? CoversTo { get; set; }
    public double? Litres { get; set; }
    public bool? FullTank { get; set; }
    public string? Station { get; set; }
    public int MaintenanceFlags { get; set; }
    public string? MaintenanceDescription { get; set; }
    public string? WorkJson { get; set; }
    public long? TyreSetId { get; set; }
    public ExpenseSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class TyreSetRow : IUserOwned
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long VehicleId { get; set; }
    public string Name { get; set; } = "";
    public TyreSeason Season { get; set; }
    public string? Description { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public bool Retired { get; set; }
}

public sealed class ImportBatchRow : IUserOwned
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long VehicleId { get; set; }
    public string FileName { get; set; } = "";
    public DateTime ImportedAt { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public DateOnly? FirstTrip { get; set; }
    public DateOnly? LastTrip { get; set; }
}

public sealed class ApiTokenRow : IUserOwned
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
