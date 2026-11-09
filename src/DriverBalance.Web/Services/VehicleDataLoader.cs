using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public sealed class VehicleDataLoader(DriverBalanceDbContext db, TimeProvider clock)
{
    /// <summary>Tracked vehicle row; throws <see cref="VehicleNotFoundException"/> when the user cannot see it.</summary>
    public async Task<VehicleRow> GetVehicleAsync(long vid, CancellationToken ct) =>
        await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vid, ct) ?? throw new VehicleNotFoundException(vid);

    /// <summary>The vehicle's time zone; an unknown id falls back to the machine's local zone.</summary>
    public static TimeZoneInfo ResolveTimeZone(string id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz) ? tz : TimeZoneInfo.Local;

    public static bool TimeZoneExists(string id) => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

    public DateOnly TodayFor(VehicleRow vehicle) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ResolveTimeZone(vehicle.TimeZone)).DateTime);

    public async Task<(VehicleRow Row, VehicleData Data, AnalysisParameters Saved)> LoadAsync(long vid, CancellationToken ct)
    {
        var row = await GetVehicleAsync(vid, ct);
        var trips = (await db.Trips.AsNoTracking().Where(t => t.VehicleId == vid).OrderBy(t => t.Start).ToListAsync(ct))
            .Select(t => t.ToDomain()).ToList();
        var expenses = (await db.Expenses.AsNoTracking().Where(e => e.VehicleId == vid).OrderBy(e => e.Date).ThenBy(e => e.Id).ToListAsync(ct))
            .Select(e => e.ToDomain()).ToList();
        var tyres = (await db.TyreSets.AsNoTracking().Where(t => t.VehicleId == vid).OrderBy(t => t.Id).ToListAsync(ct))
            .Select(t => t.ToDomain()).ToList();

        var data = new VehicleData(trips, expenses, tyres, row.HomeCurrency, TodayFor(row), row.InitialTyreSetId,
            row.HistoryStart, row.HistoryStartOdometer);
        return (row, data, Mapping.ParseParameters(row.ParametersJson));
    }
}
