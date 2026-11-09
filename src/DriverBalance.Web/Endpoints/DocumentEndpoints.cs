using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace DriverBalance.Web;

public static class DocumentEndpoints
{
    // Ten files of 20 MB plus multipart overhead; the per-file limit is checked by hand so it yields a 400, not Kestrel's 413.
    public const long RequestLimit = DocumentStore.MaxFiles * DocumentStore.MaxBytes + 4 * 1024 * 1024;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/vehicles/{vid:long}/expenses/{id:long}/documents", Upload)
            .WithMetadata(new RequestSizeLimitAttribute(RequestLimit));
        api.MapGet("/vehicles/{vid:long}/documents/{docId:long}", Download);
        api.MapDelete("/vehicles/{vid:long}/documents/{docId:long}", Delete);
    }

    public static ExpenseDocumentInfo ToInfo(this ExpenseDocumentRow d) => new(d.Id, d.FileName, d.ContentType, d.SizeBytes);

    /// <summary>Documents grouped by expense id for the given expenses of a vehicle.</summary>
    public static async Task<Dictionary<long, IReadOnlyList<ExpenseDocumentInfo>>> LoadAsync(
        DriverBalanceDbContext db, long vid, IReadOnlyCollection<long> expenseIds, CancellationToken ct)
    {
        if (expenseIds.Count == 0) return [];
        var rows = await db.ExpenseDocuments.AsNoTracking()
            .Where(d => d.VehicleId == vid && expenseIds.Contains(d.ExpenseId)).OrderBy(d => d.Id).ToListAsync(ct);
        return rows.GroupBy(d => d.ExpenseId).ToDictionary(g => g.Key, g => (IReadOnlyList<ExpenseDocumentInfo>)g.Select(d => d.ToInfo()).ToList());
    }

    private static async Task<IFormFileCollection> ReadFilesAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasFormContentType) throw new ApiValidationException("Expected a multipart/form-data request.");
        try
        {
            return (await request.ReadFormAsync(ct)).Files;
        }
        catch (Exception e) when (e is InvalidDataException or BadHttpRequestException or IOException)
        {
            throw new ApiValidationException("The upload could not be read: " + e.Message);
        }
    }

    private static async Task<IResult> Upload(long vid, long id, HttpRequest request, VehicleDataLoader loader,
        DriverBalanceDbContext db, DocumentStore store, TimeProvider clock, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        if (!await db.Expenses.AnyAsync(x => x.Id == id && x.VehicleId == vid, ct)) return Results.NotFound();

        var files = (await ReadFilesAsync(request, ct)).Where(f => f.Name == "files").ToList();
        if (files.Count == 0) throw new ApiValidationException("Attach at least one file in the 'files' field.");
        if (files.Count > DocumentStore.MaxFiles) throw new ApiValidationException($"At most {DocumentStore.MaxFiles} files per upload.");

        var errors = new List<string>();
        var rows = new List<(ExpenseDocumentRow Row, IFormFile File)>();
        foreach (var file in files)
        {
            var display = DocumentStore.DisplayName(file.FileName);
            var type = DocumentStore.NormaliseType(file.ContentType, display);
            if (type is null) errors.Add($"'{display}' is not an accepted file type (PDF, JPEG, PNG, WebP or HEIC).");
            else if (file.Length > DocumentStore.MaxBytes) errors.Add($"'{display}' is larger than {DocumentStore.MaxBytes / 1024 / 1024} MB.");
            else if (file.Length == 0) errors.Add($"'{display}' is empty.");
            else rows.Add((new ExpenseDocumentRow
            {
                VehicleId = vid, ExpenseId = id, FileName = display, StoredName = DocumentStore.SafeName(display),
                ContentType = type, SizeBytes = file.Length, CreatedAt = clock.GetUtcNow().UtcDateTime,
            }, file));
        }
        if (errors.Count > 0) throw new ApiValidationException(errors);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ExpenseDocuments.AddRange(rows.Select(r => r.Row));
        await db.SaveChangesAsync(ct);
        try
        {
            foreach (var (row, file) in rows) await store.SaveAsync(row, file, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            foreach (var (row, _) in rows) store.DeleteFile(row);
            throw;
        }
        return Results.Created($"/api/vehicles/{vid}/expenses/{id}", rows.Select(r => r.Row.ToInfo()).ToList());
    }

    private static async Task<IResult> Download(long vid, long docId, HttpContext ctx, VehicleDataLoader loader,
        DriverBalanceDbContext db, DocumentStore store, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var row = await db.ExpenseDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId && d.VehicleId == vid, ct);
        if (row is null) return Results.NotFound();
        var stream = store.Open(row);
        if (stream is null) return Results.NotFound();
        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(row.FileName);
        ctx.Response.Headers.ContentDisposition = disposition.ToString();
        ctx.Response.Headers.XContentTypeOptions = "nosniff";
        ctx.Response.Headers.ContentSecurityPolicy = "sandbox";
        return Results.File(stream, row.ContentType, enableRangeProcessing: false);
    }

    private static async Task<IResult> Delete(long vid, long docId, VehicleDataLoader loader,
        DriverBalanceDbContext db, DocumentStore store, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var row = await db.ExpenseDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.VehicleId == vid, ct);
        if (row is null) return Results.NotFound();
        db.ExpenseDocuments.Remove(row);
        await db.SaveChangesAsync(ct);
        store.DeleteFile(row);
        return Results.NoContent();
    }
}
