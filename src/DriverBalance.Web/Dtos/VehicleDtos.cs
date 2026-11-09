using System.Text.Json;
using DriverBalance.Data;

namespace DriverBalance.Web;

public sealed record VehicleDto(long Id, string Name, string HomeCurrency, string TimeZone, long? InitialTyreSetId, bool IsDefault,
    DateOnly? HistoryStart, int? HistoryStartOdometer,
    DateOnly? PurchaseDate, decimal? EstimatedResaleValue, DateOnly? ResaleEstimateDate, CarDetails Details, string? FuelType, bool HasDpf)
{
    public static VehicleDto From(VehicleRow r) => new(r.Id, r.Name, r.HomeCurrency, r.TimeZone, r.InitialTyreSetId, r.IsDefault, r.HistoryStart, r.HistoryStartOdometer,
        r.PurchaseDate, r.EstimatedResaleValue, r.ResaleEstimateDate, CarDetails.FromJson(r.DetailsJson),
        FuelTypes.Normalise(r.FuelType), FuelTypes.HasDpf(r.FuelType));
}

/// <summary>Allowed fuel types; the DPF only exists on diesels (or when the fuel type is not set).</summary>
public static class FuelTypes
{
    public static readonly string[] Allowed = ["Diesel", "DieselMhev", "Petrol", "PetrolMhev", "PetrolPhev", "Electric"];
    public static string? Normalise(string? s) => Allowed.FirstOrDefault(a => string.Equals(a, s?.Trim(), StringComparison.OrdinalIgnoreCase));
    public static bool HasDpf(string? s) => Normalise(s) is null or "Diesel" or "DieselMhev";
}

/// <summary>What the car is: shown on Overview and edited in Settings. Every field is optional.</summary>
public sealed record CarDetails(string? Model = null, string? Engine = null, string? Trim = null, int? ModelYear = null,
    string? ExteriorColour = null, string? Interior = null, IReadOnlyList<string>? Equipment = null,
    /* "Manual" or "Automatic"; null when not set. */ string? Transmission = null, bool Awd = false)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const int MaxText = 100, MaxEquipment = 60;
    public static readonly string[] Transmissions = ["Manual", "Automatic"];

    public static CarDetails FromJson(string? json) =>
        (string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<CarDetails>(json, Json)) is { } d ? d with { Equipment = d.Equipment ?? [] } : new CarDetails(Equipment: []);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Trimmed, blanks dropped, equipment de-duplicated ignoring case; problems go to errors.</summary>
    public CarDetails Normalise(List<string> errors, int currentYear)
    {
        var transmission = Transmissions.FirstOrDefault(t => string.Equals(t, Transmission?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (Clean(Transmission) is { } tr && transmission is null) errors.Add($"Transmission must be one of {string.Join(", ", Transmissions)}.");
        var d = new CarDetails(Clean(Model), Clean(Engine), Clean(Trim), ModelYear, Clean(ExteriorColour), Clean(Interior),
            (Equipment ?? []).Select(Clean).OfType<string>().DistinctBy(x => x.ToLowerInvariant()).ToList(), transmission, Awd);
        foreach (var (name, value) in new[] { ("Model", d.Model), ("Engine", d.Engine), ("Trim", d.Trim), ("Exterior colour", d.ExteriorColour), ("Interior", d.Interior) })
            if (value is { Length: > MaxText }) errors.Add($"{name} must be at most {MaxText} characters.");
        if (d.ModelYear is { } y && (y < 1950 || y > currentYear + 1)) errors.Add($"Model year must be between 1950 and {currentYear + 1}.");
        if (d.Equipment!.Count > MaxEquipment) errors.Add($"At most {MaxEquipment} equipment items are allowed.");
        if (d.Equipment.Any(e => e.Length > MaxText)) errors.Add($"Each equipment item must be at most {MaxText} characters.");
        return d;
    }
}

public sealed record VehicleInput(string? Name, string? HomeCurrency, string? TimeZone, long? InitialTyreSetId, bool? IsDefault,
    DateOnly? HistoryStart = null, int? HistoryStartOdometer = null,
    DateOnly? PurchaseDate = null, decimal? EstimatedResaleValue = null, DateOnly? ResaleEstimateDate = null,
    /* Null keeps the stored details. */ CarDetails? Details = null,
    /* Diesel, DieselMhev, Petrol, PetrolMhev, PetrolPhev, Electric; null or empty clears it. */ string? FuelType = null);
