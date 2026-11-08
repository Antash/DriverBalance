using DriverBalance.Core;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Data;

public sealed record ImportResult(long BatchId, int Imported, int Skipped, DateOnly? FirstTrip, DateOnly? LastTrip, IReadOnlyList<string> Warnings);

public sealed class ImportService(DriverBalanceDbContext db)
{
    public async Task<ImportResult> ImportCsvAsync(long vehicleId, string fileName, TextReader csv, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new InvalidOperationException($"Vehicle {vehicleId} not found.");

        var parsed = VolvoCsvParser.Parse(csv);
        var seen = (await db.Trips.Where(t => t.VehicleId == vehicleId).Select(t => new { t.Start, t.End }).ToListAsync(ct))
            .Select(t => (t.Start, t.End)).ToHashSet();

        var batch = new ImportBatchRow { VehicleId = vehicleId, FileName = fileName, ImportedAt = DateTime.UtcNow };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct); // need the batch id

        var added = new List<Trip>();
        foreach (var trip in parsed.Trips)
        {
            if (!seen.Add((trip.Start, trip.End))) continue;
            var row = trip.ToRow();
            row.VehicleId = vehicleId;
            row.ImportBatchId = batch.Id;
            db.Trips.Add(row);
            added.Add(trip);
        }

        batch.Imported = added.Count;
        batch.Skipped = parsed.Trips.Count - added.Count;
        if (added.Count > 0)
        {
            batch.FirstTrip = DateOnly.FromDateTime(added.Min(t => t.Start));
            batch.LastTrip = DateOnly.FromDateTime(added.Max(t => t.Start));
        }
        await db.SaveChangesAsync(ct);

        return new ImportResult(batch.Id, batch.Imported, batch.Skipped, batch.FirstTrip, batch.LastTrip, parsed.Warnings);
    }
}
