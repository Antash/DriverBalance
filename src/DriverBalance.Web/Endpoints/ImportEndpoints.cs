using System.Text.Json;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public sealed record ImportBatchDto(long Id, string FileName, DateTime ImportedAt, int Imported, int Skipped, DateOnly? FirstTrip, DateOnly? LastTrip);

/// <summary>Newest trip start date over all trips of the vehicle (independent of import batches).</summary>
public sealed record LastTripDto(DateOnly? LastTrip);

public static class ImportEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var i = api.MapGroup("/vehicles/{vid:long}/import");
        i.MapPost("/csv", ImportCsv);
        i.MapGet("/batches", Batches);
        i.MapGet("/last-trip", LastTrip);
    }

    private static async Task<IFormFileCollection> FilesAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasFormContentType) throw new ApiValidationException("Expected a multipart/form-data request.");
        return (await request.ReadFormAsync(ct)).Files;
    }

    private static async Task<IResult> ImportCsv(long vid, HttpRequest request, VehicleDataLoader loader, ImportService import, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var file = (await FilesAsync(request, ct)).GetFile("file") ?? throw new ApiValidationException("Missing form file 'file'.");
        using var reader = new StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Results.Ok(await import.ImportCsvAsync(vid, file.FileName, reader, ct));
    }

    private static async Task<IResult> Batches(long vid, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var rows = await db.ImportBatches.AsNoTracking().Where(b => b.VehicleId == vid).OrderByDescending(b => b.Id).ToListAsync(ct);
        return Results.Ok(rows.Select(b => new ImportBatchDto(b.Id, b.FileName, DateTime.SpecifyKind(b.ImportedAt, DateTimeKind.Utc), b.Imported, b.Skipped, b.FirstTrip, b.LastTrip)).ToList());
    }

    private static async Task<IResult> LastTrip(long vid, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var last = await db.Trips.Where(t => t.VehicleId == vid).MaxAsync(t => (DateTime?)t.Start, ct);
        return Results.Ok(new LastTripDto(last is { } d ? DateOnly.FromDateTime(d) : null));
    }
}
