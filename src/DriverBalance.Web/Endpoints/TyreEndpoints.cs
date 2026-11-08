using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public static class TyreEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var t = api.MapGroup("/vehicles/{vid:long}/tyres");
        t.MapGet("", List);
        t.MapGet("/summary", Summary);
        t.MapPost("", Create);
        t.MapPut("/{id:long}", Update);
        t.MapDelete("/{id:long}", Delete);
    }

    private static async Task<IResult> List(long vid, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var rows = await db.TyreSets.AsNoTracking().Where(t => t.VehicleId == vid).OrderBy(t => t.Id).ToListAsync(ct);
        return Results.Ok(rows.Select(r => r.ToDomain()).ToList());
    }

    private static async Task<IResult> Summary(long vid, AnalysisSessionFactory sessions, CancellationToken ct)
    {
        var s = await sessions.OpenAsync(vid, null, ct);
        var spans = TyreMileage.Spans(s.Data.Expenses, s.Data.InitialTyreSetId, s.Context.Timeline, s.Data.Today);
        var summaries = TyreMileage.Summaries(spans, s.Data.TyreSets, s.Data.Expenses, s.Data.HomeCurrency);
        return Results.Ok(new TyreSummaryResponse(summaries, spans.Where(x => x.TyreSetId is null).ToList()));
    }

    private static async Task<IResult> Create(long vid, TyreSet body, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        Validate(body);
        var row = (body with { Id = 0 }).ToRow();
        row.VehicleId = vid;
        db.TyreSets.Add(row);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/vehicles/{vid}/tyres/{row.Id}", row.ToDomain());
    }

    private static async Task<IResult> Update(long vid, long id, TyreSet body, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var row = await db.TyreSets.FirstOrDefaultAsync(t => t.Id == id && t.VehicleId == vid, ct);
        if (row is null) return Results.NotFound();
        Validate(body);
        row.Name = body.Name.Trim(); row.Season = body.Season; row.Description = body.Description;
        row.PurchaseDate = body.PurchaseDate; row.Retired = body.Retired;
        await db.SaveChangesAsync(ct);
        return Results.Ok(row.ToDomain());
    }

    private static async Task<IResult> Delete(long vid, long id, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var row = await db.TyreSets.FirstOrDefaultAsync(t => t.Id == id && t.VehicleId == vid, ct);
        if (row is null) return Results.NotFound();

        int used = await db.Expenses.CountAsync(e => e.VehicleId == vid && e.TyreSetId == id, ct);
        if (used > 0)
            return Results.Json(new ErrorsResponse([$"Tyre set is used by {used} expenses; retire it instead"]), statusCode: StatusCodes.Status409Conflict);

        db.TyreSets.Remove(row);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static void Validate(TyreSet t)
    {
        if (string.IsNullOrWhiteSpace(t.Name)) throw new ApiValidationException("Name is required.");
    }
}
