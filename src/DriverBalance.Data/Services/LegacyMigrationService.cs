using DriverBalance.Core;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Data;

public sealed record LegacyMigrationResult(int TripsImported, int TripsSkipped, int ExpensesImported, int ExpensesSkipped);

public sealed class LegacyMigrationService(DriverBalanceDbContext db)
{
    public const string ZeroCostNote = "imported — check cost";

    private sealed record ExpenseKey(DateOnly Date, int Flags, int? Odometer, decimal Amount, string Description);

    public async Task<LegacyMigrationResult> MigrateAsync(long vehicleId, Stream? tripsJson, Stream? maintenanceJson, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new InvalidOperationException($"Vehicle {vehicleId} not found.");

        int tripsImported = 0, tripsSkipped = 0, expensesImported = 0, expensesSkipped = 0;

        if (tripsJson != null)
        {
            var seen = (await db.Trips.Where(t => t.VehicleId == vehicleId).Select(t => new { t.Start, t.End }).ToListAsync(ct))
                .Select(t => (t.Start, t.End)).ToHashSet();
            var newTrips = new List<TripRow>();
            foreach (var trip in LegacyJsonReader.ReadTrips(tripsJson))
            {
                if (!seen.Add((trip.Start, trip.End))) { tripsSkipped++; continue; }
                var row = trip.ToRow();
                row.VehicleId = vehicleId;
                db.Trips.Add(row);
                newTrips.Add(row);
                tripsImported++;
            }
            if (newTrips.Count > 0)
            {
                // Shown in the import history like any other import; linked so the trips can be traced to it.
                var batch = new ImportBatchRow
                {
                    VehicleId = vehicleId, FileName = "legacy triprecords.json", ImportedAt = DateTime.UtcNow,
                    Imported = newTrips.Count, Skipped = tripsSkipped,
                    FirstTrip = DateOnly.FromDateTime(newTrips.Min(t => t.Start)),
                    LastTrip = DateOnly.FromDateTime(newTrips.Max(t => t.Start)),
                };
                db.ImportBatches.Add(batch);
                await db.SaveChangesAsync(ct); // need the batch id
                foreach (var row in newTrips) row.ImportBatchId = batch.Id;
            }
        }

        if (maintenanceJson != null)
        {
            var existing = await db.Expenses
                .Where(e => e.VehicleId == vehicleId && e.Category == ExpenseCategory.Maintenance)
                .ToListAsync(ct);
            var seen = existing.Select(KeyOf).ToHashSet();
            foreach (var expense in LegacyJsonReader.ReadMaintenance(maintenanceJson))
            {
                var row = expense.ToRow();
                if (!seen.Add(KeyOf(row))) { expensesSkipped++; continue; }
                row.VehicleId = vehicleId;
                if (row.Amount == 0) row.Note = ZeroCostNote;
                db.Expenses.Add(row);
                expensesImported++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new LegacyMigrationResult(tripsImported, tripsSkipped, expensesImported, expensesSkipped);
    }

    private static ExpenseKey KeyOf(ExpenseRow e) =>
        new(e.Date, e.MaintenanceFlags, e.Odometer, e.Amount, e.MaintenanceDescription ?? "");
}
