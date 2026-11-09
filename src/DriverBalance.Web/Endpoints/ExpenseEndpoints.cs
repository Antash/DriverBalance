using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public static class ExpenseEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var e = api.MapGroup("/vehicles/{vid:long}/expenses");
        e.MapGet("", List);
        e.MapPost("", Create);
        e.MapPut("/{id:long}", Update);
        e.MapDelete("/{id:long}", Delete);
    }

    /// <summary>Filters use the same half-open convention as periods: from inclusive, to exclusive.</summary>
    private static async Task<IResult> List(long vid, DateOnly? from, DateOnly? to, string? category,
        VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        ExpenseCategory? cat = null;
        if (!string.IsNullOrEmpty(category))
        {
            if (!Enum.TryParse<ExpenseCategory>(category, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ApiValidationException($"Unknown category '{category}'.");
            cat = parsed;
        }

        var query = db.Expenses.AsNoTracking().Where(x => x.VehicleId == vid);
        if (from is { } f) query = query.Where(x => x.Date >= f);
        if (to is { } t) query = query.Where(x => x.Date < t);
        if (cat is { } c) query = query.Where(x => x.Category == c);
        var rows = await query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync(ct);
        return Results.Ok(rows.Select(r => r.ToDomain()).ToList());
    }

    private static async Task<IResult> Create(long vid, Expense body, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var expense = await ExpenseRules.ValidateAsync(vid, body, db, ct);
        var row = (expense with { Id = 0, Source = ExpenseSource.Web }).ToRow();
        row.VehicleId = vid;
        db.Expenses.Add(row);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/vehicles/{vid}/expenses/{row.Id}", row.ToDomain());
    }

    private static async Task<IResult> Update(long vid, long id, Expense body, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var row = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.VehicleId == vid, ct);
        if (row is null) return Results.NotFound();

        var e = await ExpenseRules.ValidateAsync(vid, body, db, ct);
        row.Date = e.Date; row.Category = e.Category; row.Amount = e.Amount; row.Currency = e.Currency; row.HomeAmount = e.HomeAmount;
        row.Odometer = e.Odometer; row.Note = e.Note; row.CoversFrom = e.CoversFrom; row.CoversTo = e.CoversTo; row.Litres = e.Litres;
        row.FullTank = e.FullTank; row.Station = e.Station; row.MaintenanceFlags = (int)e.Maintenance;
        row.MaintenanceDescription = e.MaintenanceDescription; row.TyreSetId = e.TyreSetId;
        row.WorkJson = e.ToRow().WorkJson;
        await db.SaveChangesAsync(ct);
        return Results.Ok(row.ToDomain());
    }

    private static async Task<IResult> Delete(long vid, long id, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var row = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.VehicleId == vid, ct);
        if (row is null) return Results.NotFound();
        db.Expenses.Remove(row);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
