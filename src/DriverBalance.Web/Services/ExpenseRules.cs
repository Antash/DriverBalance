using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

/// <summary>The one set of expense validation rules, shared by the HTTP endpoints and the MCP tools.</summary>
public static class ExpenseRules
{
    /// <summary>
    /// Returns the expense with a normalised currency, or throws a 400-style <see cref="ApiValidationException"/> listing every problem.
    /// Amount must be greater than 0, except that Maintenance may be exactly 0 (free or unknown-cost services, free tyre swaps,
    /// migrated records); negatives are always rejected.
    /// </summary>
    public static async Task<Expense> ValidateAsync(long vid, Expense e, DriverBalanceDbContext db, CancellationToken ct)
    {
        var errors = new List<string>();
        if (e.Amount < 0) errors.Add("Amount must not be negative.");
        else if (e.Amount == 0 && e.Category != ExpenseCategory.Maintenance) errors.Add("Amount must be greater than 0.");
        if (!VehicleEndpoints.IsCurrency(e.Currency ?? "")) errors.Add("Currency must be a 3-letter code.");
        if (e.HomeAmount is < 0) errors.Add("Home amount must not be negative.");
        if (e.Litres is { } l && !(l > 0)) errors.Add("Litres must be greater than 0.");
        if (e.CoversFrom is { } from && e.CoversTo is { } to && from > to) errors.Add("CoversFrom must not be after CoversTo.");
        if (e.TyreSetId is { } setId && !await db.TyreSets.AnyAsync(t => t.Id == setId && t.VehicleId == vid, ct))
            errors.Add("Tyre set does not exist on this vehicle.");
        var work = NormaliseWork(e, errors);
        if (errors.Count > 0) throw new ApiValidationException(errors);
        var flags = work.Count > 0 ? work.Aggregate(MaintenanceType.Custom, (acc, w) => acc | (w.Type ?? MaintenanceType.Custom)) : e.Maintenance;
        return e with { Currency = e.Currency!.ToUpperInvariant(), Work = work, Maintenance = flags };
    }

    private const int MaxWorkItems = 50, MaxNameLength = 100, MaxBrandLength = 100, MaxDetailsLength = 200;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static List<MaintenanceWork> NormaliseWork(Expense e, List<string> errors)
    {
        var result = new List<MaintenanceWork>();
        var items = e.Work ?? [];
        if (items.Count == 0) return result;
        if (e.Category != ExpenseCategory.Maintenance) { errors.Add("Work items are only allowed on Maintenance expenses."); return result; }
        if (items.Count > MaxWorkItems) { errors.Add($"At most {MaxWorkItems} work items are allowed."); return result; }

        var seen = new HashSet<MaintenanceType>();
        for (var i = 0; i < items.Count; i++)
        {
            var n = $"Work item {i + 1}";
            if (items[i] is not { } w) { errors.Add($"{n} is empty."); continue; }
            var name = Clean(w.Name); var brand = Clean(w.Brand); var model = Clean(w.Model); var details = Clean(w.Details);
            if (w.Type is { } t)
            {
                if (t == MaintenanceType.Custom || !Enum.IsDefined(t) || !System.Numerics.BitOperations.IsPow2((int)t))
                    errors.Add($"{n}: type must be a single maintenance type; use no type for a custom item.");
                else if (!seen.Add(t)) errors.Add($"{n}: {t} appears more than once.");
            }
            else if (name is null) errors.Add($"{n}: a custom item needs a name.");
            if (name is { Length: > MaxNameLength }) errors.Add($"{n}: name must be at most {MaxNameLength} characters.");
            if (!Enum.IsDefined(w.Part)) errors.Add($"{n}: unknown part source.");
            if (w.Part == PartSource.Aftermarket)
            {
                if (brand is null) errors.Add($"{n}: aftermarket parts need a brand.");
            }
            else { brand = null; model = null; }
            if (brand is { Length: > MaxBrandLength }) errors.Add($"{n}: brand must be at most {MaxBrandLength} characters.");
            if (model is { Length: > MaxBrandLength }) errors.Add($"{n}: model must be at most {MaxBrandLength} characters.");
            if (details is { Length: > MaxDetailsLength }) errors.Add($"{n}: details must be at most {MaxDetailsLength} characters.");
            result.Add(w with { Name = name, Brand = brand, Model = model, Details = details });
        }
        return result;
    }
}
