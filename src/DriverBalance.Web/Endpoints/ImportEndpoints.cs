using System.Text.Json;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public sealed record ImportBatchDto(long Id, string FileName, DateTime ImportedAt, int Imported, int Skipped, DateOnly? FirstTrip, DateOnly? LastTrip);

public static class ImportEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var i = api.MapGroup("/vehicles/{vid:long}/import");
        i.MapPost("/csv", ImportCsv);
        i.MapPost("/legacy", ImportLegacy);
        i.MapGet("/batches", Batches);
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

    private static async Task<IResult> ImportLegacy(long vid, HttpRequest request, VehicleDataLoader loader, LegacyMigrationService migration, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var files = await FilesAsync(request, ct);
        var trips = files.GetFile("trips");
        var maintenance = files.GetFile("maintenance");
        if (trips is null && maintenance is null) throw new ApiValidationException("Provide a 'trips' and/or 'maintenance' file.");

        try
        {
            await using var tripsStream = trips?.OpenReadStream();
            await using var maintenanceStream = maintenance?.OpenReadStream();
            return Results.Ok(await migration.MigrateAsync(vid, tripsStream, maintenanceStream, ct));
        }
        catch (JsonException e)
        {
            throw new ApiValidationException($"Invalid JSON file: {e.Message}");
        }
    }

    private static async Task<IResult> Batches(long vid, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var rows = await db.ImportBatches.AsNoTracking().Where(b => b.VehicleId == vid).OrderByDescending(b => b.Id).ToListAsync(ct);
        return Results.Ok(rows.Select(b => new ImportBatchDto(b.Id, b.FileName, DateTime.SpecifyKind(b.ImportedAt, DateTimeKind.Utc), b.Imported, b.Skipped, b.FirstTrip, b.LastTrip)).ToList());
    }
}
