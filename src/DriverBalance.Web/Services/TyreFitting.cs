using System.Text.Json;
using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

/// <summary>Request-only description of the tyres fitted by a tyre-swap expense.</summary>
public sealed record TyreFittingRequest
{
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public string? Size { get; init; }
    public TyreSeason Season { get; init; }
    public bool IsNew { get; init; }
}

/// <summary>Tyre set normalisation (derived name, length limits) and fitting a set from a tyre-swap expense.</summary>
public static class TyreFitting
{
    private const int MaxLength = 100;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static string SeasonLabel(TyreSeason s) => s switch { TyreSeason.AllSeason => "All-season", _ => s.ToString() };

    /// <summary>Trims fields, derives a blank name ("Brand Model", else "Summer tyres"), and throws a 400 when something is invalid.</summary>
    public static TyreSet Normalise(TyreSet t)
    {
        var errors = new List<string>();
        var brand = Clean(t.Brand); var model = Clean(t.Model); var size = Clean(t.Size);
        var name = Clean(t.Name);
        if (name is null)
        {
            var derived = Clean($"{brand} {model}") ?? $"{SeasonLabel(t.Season)} tyres";
            name = derived.Length > MaxLength ? derived[..MaxLength].TrimEnd() : derived;
        }
        if (!Enum.IsDefined(t.Season)) errors.Add("Unknown season.");
        if (name.Length > MaxLength) errors.Add($"Name must be at most {MaxLength} characters.");
        if (brand is { Length: > MaxLength }) errors.Add($"Brand must be at most {MaxLength} characters.");
        if (model is { Length: > MaxLength }) errors.Add($"Model must be at most {MaxLength} characters.");
        if (size is { Length: > MaxLength }) errors.Add($"Size must be at most {MaxLength} characters.");
        if (errors.Count > 0) throw new ApiValidationException(errors);
        return t with { Name = name, Brand = brand, Model = model, Size = size };
    }

    /// <summary>Reads an expense request body: the expense plus the optional request-only <c>tyres</c> object.</summary>
    public static (Expense Expense, TyreFittingRequest? Tyres) ReadBody(JsonElement body)
    {
        try
        {
            if (body.ValueKind != JsonValueKind.Object) throw new ApiValidationException("A JSON object body is required.");
            var expense = body.Deserialize<Expense>(ApiJson.Options) ?? throw new ApiValidationException("A JSON object body is required.");
            TyreFittingRequest? tyres = null;
            foreach (var p in body.EnumerateObject())
                if (string.Equals(p.Name, "tyres", StringComparison.OrdinalIgnoreCase))
                    tyres = p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.Deserialize<TyreFittingRequest>(ApiJson.Options);
            return (expense, tyres);
        }
        catch (JsonException ex) { throw new ApiValidationException("Invalid request body: " + ex.Message); }
    }

    /// <summary>Checks the request against the validated expense and returns the normalised set to create.</summary>
    public static TyreSet Check(Expense validated, TyreFittingRequest t)
    {
        if (validated.Category != ExpenseCategory.Maintenance || (validated.Maintenance & MaintenanceType.TyreSwap) == 0)
            throw new ApiValidationException("Tyres can only be given on a Maintenance expense with a tyre swap.");
        if (validated.TyreSetId is not null)
            throw new ApiValidationException("Give either tyres or a tyre set, not both.");
        return Normalise(new TyreSet { Brand = t.Brand, Model = t.Model, Size = t.Size, Season = t.Season });
    }

    /// <summary>The set fitted just before (date, id): the latest earlier mount's set, else the vehicle's initial set.</summary>
    public static async Task<long?> FittedBeforeAsync(long vid, DateOnly date, long? excludeId, DriverBalanceDbContext db, CancellationToken ct)
    {
        int mask = (int)TyreMileage.MountFlags;
        var prev = await db.Expenses.AsNoTracking()
            .Where(x => x.VehicleId == vid && x.Category == ExpenseCategory.Maintenance && (x.MaintenanceFlags & mask) != 0
                        && x.Id != excludeId && (x.Date < date || (x.Date == date && (excludeId == null || x.Id < excludeId))))
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Select(x => new { x.TyreSetId }).FirstOrDefaultAsync(ct);
        if (prev is not null) return prev.TyreSetId;
        return await db.Vehicles.AsNoTracking().Where(v => v.Id == vid).Select(v => v.InitialTyreSetId).FirstOrDefaultAsync(ct);
    }

    /// <summary>Inserts the new set and, for new tyres, retires the previously fitted one. Returns the new set's id.</summary>
    public static async Task<long> CreateAndRetireAsync(long vid, TyreSet set, bool isNew, DateOnly date, long? expenseId,
        DriverBalanceDbContext db, CancellationToken ct)
    {
        var previous = isNew ? await FittedBeforeAsync(vid, date, expenseId, db, ct) : null;
        var row = (set with { Id = 0, Retired = false }).ToRow();
        row.VehicleId = vid;
        db.TyreSets.Add(row);
        await db.SaveChangesAsync(ct);
        if (previous is { } p && p != row.Id)
        {
            var old = await db.TyreSets.FirstOrDefaultAsync(x => x.Id == p && x.VehicleId == vid, ct);
            if (old is not null) old.Retired = true;
        }
        return row.Id;
    }
}
