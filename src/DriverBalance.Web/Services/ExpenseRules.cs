using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

/// <summary>The one set of expense validation rules, shared by the HTTP endpoints and the MCP tools.</summary>
public static class ExpenseRules
{
    /// <summary>
    /// Returns the expense with a normalised currency, or throws a 400-style <see cref="ApiValidationException"/> listing every problem.
    /// <paramref name="allowZeroAmount"/> exists for free actions such as a tyre swap done at home; negatives are always rejected.
    /// </summary>
    public static async Task<Expense> ValidateAsync(long vid, Expense e, DriverBalanceDbContext db, CancellationToken ct, bool allowZeroAmount = false)
    {
        var errors = new List<string>();
        if (allowZeroAmount ? e.Amount < 0 : e.Amount <= 0)
            errors.Add(allowZeroAmount ? "Amount must not be negative." : "Amount must be greater than 0.");
        if (!VehicleEndpoints.IsCurrency(e.Currency ?? "")) errors.Add("Currency must be a 3-letter code.");
        if (e.HomeAmount is < 0) errors.Add("Home amount must not be negative.");
        if (e.Litres is { } l && !(l > 0)) errors.Add("Litres must be greater than 0.");
        if (e.CoversFrom is { } from && e.CoversTo is { } to && from > to) errors.Add("CoversFrom must not be after CoversTo.");
        if (e.TyreSetId is { } setId && !await db.TyreSets.AnyAsync(t => t.Id == setId && t.VehicleId == vid, ct))
            errors.Add("Tyre set does not exist on this vehicle.");
        if (errors.Count > 0) throw new ApiValidationException(errors);
        return e with { Currency = e.Currency!.ToUpperInvariant() };
    }
}
