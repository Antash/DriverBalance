using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

/// <summary>
/// The lump-sum purchase costs of a car (price, warranty, registration transfer), edited in Settings.
/// Stored as Purchase-category expenses in the vehicle's home currency; the general expense endpoints refuse new ones.
/// </summary>
public static class PurchaseCostEndpoints
{
    public const int MaxNoteLength = 200;
    public const decimal MaxAmount = 1e12m;

    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/vehicles/{vid:long}/purchase-costs");
        g.MapGet("", List);
        g.MapPost("", Create);
        g.MapPut("/{id:long}", Update);
        g.MapDelete("/{id:long}", Delete);
    }

    /// <summary>Currency is optional: a new cost defaults to the home currency, an edit keeps the stored one.</summary>
    public sealed record PurchaseCostInput(DateOnly? Date, decimal? Amount, string? Note, string? Currency = null);
    /// <summary>Amount is in the stored currency; HomeAmount is what Ownership counts (null when unknown).</summary>
    public sealed record PurchaseCostDto(long Id, DateOnly Date, decimal Amount, string Currency, decimal? HomeAmount, string? Note);

    private static PurchaseCostDto ToDto(ExpenseRow r, string home) =>
        new(r.Id, r.Date, r.Amount, r.Currency, string.Equals(r.Currency, home, StringComparison.OrdinalIgnoreCase) ? r.Amount : r.HomeAmount, r.Note);

    private static (DateOnly Date, decimal Amount, string? Note, string? Currency) Validate(PurchaseCostInput input)
    {
        var errors = new List<string>();
        if (input.Date is null) errors.Add("Date is required.");
        if (input.Amount is not > 0) errors.Add("Amount must be greater than 0.");
        else if (input.Amount > MaxAmount) errors.Add("Amount is too large.");
        if (input.Currency is { } cur && !VehicleEndpoints.IsCurrency(cur)) errors.Add("Currency must be a 3-letter code.");
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        if (note is { Length: > MaxNoteLength }) errors.Add($"Note must be at most {MaxNoteLength} characters.");
        if (errors.Count > 0) throw new ApiValidationException(errors);
        return (input.Date!.Value, input.Amount!.Value, note, input.Currency?.ToUpperInvariant());
    }

    private static async Task<IResult> List(long vid, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        var home = (await loader.GetVehicleAsync(vid, ct)).HomeCurrency;
        var rows = await db.Expenses.AsNoTracking().Where(x => x.VehicleId == vid && x.Category == ExpenseCategory.Purchase)
            .OrderBy(x => x.Date).ThenBy(x => x.Id).ToListAsync(ct);
        return Results.Ok(rows.Select(r => ToDto(r, home)).ToList());
    }

    private static async Task<IResult> Create(long vid, PurchaseCostInput input, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        var home = (await loader.GetVehicleAsync(vid, ct)).HomeCurrency;
        var (date, amount, note, currency) = Validate(input);
        var row = new ExpenseRow
        {
            VehicleId = vid, Date = date, Category = ExpenseCategory.Purchase, Amount = amount, Currency = currency ?? home, Note = note,
            Source = ExpenseSource.Web, CreatedAt = DateTime.UtcNow,
        };
        db.Expenses.Add(row);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/vehicles/{vid}/purchase-costs/{row.Id}", ToDto(row, home));
    }

    private static async Task<IResult> Update(long vid, long id, PurchaseCostInput input, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        var home = (await loader.GetVehicleAsync(vid, ct)).HomeCurrency;
        var row = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.VehicleId == vid && x.Category == ExpenseCategory.Purchase, ct);
        if (row is null) return Results.NotFound();
        var (date, amount, note, currency) = Validate(input);
        currency ??= row.Currency;
        // A home-currency row needs no conversion; a changed foreign currency invalidates the stored one.
        if (string.Equals(currency, home, StringComparison.OrdinalIgnoreCase) || !string.Equals(currency, row.Currency, StringComparison.OrdinalIgnoreCase)) row.HomeAmount = null;
        row.Date = date; row.Amount = amount; row.Note = note; row.Currency = currency;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(row, home));
    }

    private static async Task<IResult> Delete(long vid, long id, VehicleDataLoader loader, DriverBalanceDbContext db, DocumentStore documents, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var row = await db.Expenses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.VehicleId == vid && x.Category == ExpenseCategory.Purchase, ct);
        return row is not null && await ExpenseDeletion.DeleteAsync(vid, id, db, documents, ct) ? Results.NoContent() : Results.NotFound();
    }
}
