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

    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/vehicles/{vid:long}/purchase-costs");
        g.MapGet("", List);
        g.MapPost("", Create);
        g.MapPut("/{id:long}", Update);
        g.MapDelete("/{id:long}", Delete);
    }

    public sealed record PurchaseCostInput(DateOnly? Date, decimal? Amount, string? Note);
    public sealed record PurchaseCostDto(long Id, DateOnly Date, decimal Amount, string? Note);

    private static PurchaseCostDto ToDto(ExpenseRow r, string home) =>
        new(r.Id, r.Date, string.Equals(r.Currency, home, StringComparison.OrdinalIgnoreCase) ? r.Amount : r.HomeAmount ?? r.Amount, r.Note);

    private static (DateOnly Date, decimal Amount, string? Note) Validate(PurchaseCostInput input)
    {
        var errors = new List<string>();
        if (input.Date is null) errors.Add("Date is required.");
        if (input.Amount is not > 0) errors.Add("Amount must be greater than 0.");
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        if (note is { Length: > MaxNoteLength }) errors.Add($"Note must be at most {MaxNoteLength} characters.");
        if (errors.Count > 0) throw new ApiValidationException(errors);
        return (input.Date!.Value, input.Amount!.Value, note);
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
        var (date, amount, note) = Validate(input);
        var row = new ExpenseRow
        {
            VehicleId = vid, Date = date, Category = ExpenseCategory.Purchase, Amount = amount, Currency = home, Note = note,
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
        var (date, amount, note) = Validate(input);
        row.Date = date; row.Amount = amount; row.Note = note; row.Currency = home; row.HomeAmount = null;
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
