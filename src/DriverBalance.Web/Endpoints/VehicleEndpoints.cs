using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;

namespace DriverBalance.Web;

public static class VehicleEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var v = api.MapGroup("/vehicles");
        v.MapGet("", List);
        v.MapPost("", Create);
        v.MapGet("/{vid:long}", Get);
        v.MapPut("/{vid:long}", Update);
        v.MapGet("/{vid:long}/ownership", GetOwnership);

        v.MapGet("/{vid:long}/parameters", GetParameters);
        v.MapPut("/{vid:long}/parameters", PutParameters);
        v.MapGet("/{vid:long}/odometer/latest", LatestOdometer);
        api.MapGet("/parameters/defaults", () => Results.Ok(new AnalysisParameters()));
    }

    /// <summary>Highest odometer reading across trips (end odometer) and expenses, with the date it was taken; 204 when none.</summary>
    private static async Task<IResult> LatestOdometer(long vid, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        await loader.GetVehicleAsync(vid, ct);
        var trip = await db.Trips.AsNoTracking().Where(t => t.VehicleId == vid && t.EndOdometer > 0)
            .OrderByDescending(t => t.EndOdometer).ThenByDescending(t => t.End).Select(t => new { Odo = t.EndOdometer, t.End }).FirstOrDefaultAsync(ct);
        var exp = await db.Expenses.AsNoTracking().Where(x => x.VehicleId == vid && x.Odometer > 0)
            .OrderByDescending(x => x.Odometer).ThenByDescending(x => x.Date).Select(x => new { Odo = x.Odometer!.Value, x.Date }).FirstOrDefaultAsync(ct);
        if (trip is null && exp is null) return Results.NoContent();
        if (exp is null || (trip is not null && trip.Odo > exp.Odo)) return Results.Ok(new OdometerReading(trip!.Odo, DateOnly.FromDateTime(trip.End)));
        return Results.Ok(new OdometerReading(exp.Odo, exp.Date));
    }

    private sealed record OdometerReading(int Odometer, DateOnly Date);

    private static async Task<IResult> GetOwnership(long vid, AnalysisSessionFactory sessions, CancellationToken ct) =>
        Results.Ok(Ownership.Compute((await sessions.OpenAsync(vid, null, ct)).Context));

    private static async Task<IResult> List(DriverBalanceDbContext db, CancellationToken ct) =>
        Results.Ok((await db.Vehicles.AsNoTracking().OrderBy(v => v.Id).ToListAsync(ct)).Select(VehicleDto.From).ToList());

    private static async Task<IResult> Get(long vid, VehicleDataLoader loader, CancellationToken ct) =>
        Results.Ok(VehicleDto.From(await loader.GetVehicleAsync(vid, ct)));

    private static async Task<IResult> Create(VehicleInput input, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        var errors = Validate(input, loader.TodayFor(new VehicleRow { TimeZone = input.TimeZone ?? new VehicleRow().TimeZone }));
        if (input.InitialTyreSetId is not null) errors.Add("Initial tyre set can only be set once the vehicle has tyre sets.");
        if (errors.Count > 0) throw new ApiValidationException(errors);

        var row = new VehicleRow { FuelType = "", ParametersJson = new AnalysisParameters().ToJson() };
        bool first = !await db.Vehicles.AnyAsync(ct);
        Apply(input, row);
        row.IsDefault = first || input.IsDefault == true;
        if (row.IsDefault) await ClearOtherDefaults(db, 0, ct);
        db.Vehicles.Add(row);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/vehicles/{row.Id}", VehicleDto.From(row));
    }

    private static async Task<IResult> Update(long vid, VehicleInput input, VehicleDataLoader loader, DriverBalanceDbContext db, CancellationToken ct)
    {
        var row = await loader.GetVehicleAsync(vid, ct);
        var errors = Validate(input, loader.TodayFor(new VehicleRow { TimeZone = input.TimeZone ?? row.TimeZone }));
        if (input.InitialTyreSetId is { } setId && !await db.TyreSets.AnyAsync(t => t.Id == setId && t.VehicleId == vid, ct))
            errors.Add("Initial tyre set does not exist on this vehicle.");
        if (input.HistoryStart is not null && input.HistoryStartOdometer is > 0 and var hsKm)
        {
            var tripMin = await db.Trips.Where(t => t.VehicleId == vid && t.StartOdometer > 0).Select(t => (int?)t.StartOdometer).MinAsync(ct);
            var expMin = await db.Expenses.Where(e => e.VehicleId == vid && e.Odometer > 0).Select(e => e.Odometer).MinAsync(ct);
            var first = new[] { tripMin, expMin }.Where(x => x is not null).Min();
            if (first is { } firstKm && hsKm > firstKm)
                errors.Add($"The odometer at the history start can't be higher than the first recorded reading ({firstKm} km).");
        }
        if (errors.Count > 0) throw new ApiValidationException(errors);

        Apply(input, row);
        row.InitialTyreSetId = input.InitialTyreSetId;
        if (input.IsDefault is { } isDefault)
        {
            row.IsDefault = isDefault;
            if (isDefault) await ClearOtherDefaults(db, vid, ct);
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(VehicleDto.From(row));
    }

    private static List<string> Validate(VehicleInput input, DateOnly today)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name)) errors.Add("Name is required.");
        if (input.HomeCurrency is { } cur && !IsCurrency(cur)) errors.Add("Home currency must be a 3-letter code.");
        if (input.TimeZone is { } tz && !VehicleDataLoader.TimeZoneExists(tz)) errors.Add($"Unknown time zone '{tz}'.");
        if (input.HistoryStartOdometer is < 0) errors.Add("History start odometer must not be negative.");
        if (input.HistoryStart is { } hs && hs > today) errors.Add("History start must not be in the future.");
        if (input.EstimatedResaleValue is < 0) errors.Add("Estimated resale value must not be negative.");
        if (input.PurchaseDate is { } pd && pd > today) errors.Add("Purchase date must not be in the future.");
        if (input.ResaleEstimateDate is { } rd && rd > today) errors.Add("Resale estimate date must not be in the future.");
        if (!string.IsNullOrWhiteSpace(input.FuelType) && FuelTypes.Normalise(input.FuelType) is null)
            errors.Add($"Fuel type must be one of {string.Join(", ", FuelTypes.Allowed)}.");
        ValidateFinancing(input, errors);
        input.Details?.Normalise(errors, today.Year);
        return errors;
    }

    public const int MaxFinancingPayments = 600;

    private static bool HasFinancing(VehicleInput i) => i.FinancingStart is not null || i.FinancingAmount is not null || i.FinancingPayments is not null;

    private static void ValidateFinancing(VehicleInput i, List<string> errors)
    {
        if (!HasFinancing(i)) return;
        if (i.FinancingStart is null) errors.Add("Financing start date is required.");
        if (i.FinancingAmount is null) errors.Add("Financing amount per payment is required.");
        else if (i.FinancingAmount <= 0) errors.Add("Financing amount per payment must be greater than 0.");
        if (i.FinancingPayments is null) errors.Add("Financing number of payments is required.");
        else if (i.FinancingPayments is < 1 or > MaxFinancingPayments) errors.Add($"Financing number of payments must be between 1 and {MaxFinancingPayments}.");
        if (i.FinancingBalloon is < 0) errors.Add("Financing final payment must not be negative.");
        if (i.FinancingFrequency is { } f && !Enum.IsDefined(f)) errors.Add("Financing frequency must be Monthly, Quarterly or Yearly.");
    }

    /// <summary>Name is required (validated); currency and time zone keep their current or default value when omitted.</summary>
    private static void Apply(VehicleInput input, VehicleRow row)
    {
        row.Name = input.Name!.Trim();
        if (input.HomeCurrency is { } cur) row.HomeCurrency = cur.ToUpperInvariant();
        if (input.TimeZone is { } tz) row.TimeZone = tz;
        // A date without an odometer means a new car (0 km); no date clears both.
        row.HistoryStart = input.HistoryStart;
        row.HistoryStartOdometer = input.HistoryStart is null ? null : input.HistoryStartOdometer ?? 0;
        row.FuelType = FuelTypes.Normalise(input.FuelType) ?? "";
        row.PurchaseDate = input.PurchaseDate;
        row.EstimatedResaleValue = input.EstimatedResaleValue;
        row.ResaleEstimateDate = input.EstimatedResaleValue is null ? null : input.ResaleEstimateDate;
        var financed = HasFinancing(input);
        row.FinancingStart = input.FinancingStart;
        row.FinancingAmount = input.FinancingAmount;
        row.FinancingPayments = input.FinancingPayments;
        row.FinancingFrequency = financed ? input.FinancingFrequency ?? FinancingFrequency.Monthly : null;
        row.FinancingBalloon = financed ? input.FinancingBalloon : null;
        if (input.Details is { } details) row.DetailsJson = details.Normalise([], DateTime.Today.Year + 1).ToJson();
    }

    public static bool IsCurrency(string s) => s.Length == 3 && s.All(char.IsAsciiLetter);

    private static async Task ClearOtherDefaults(DriverBalanceDbContext db, long exceptId, CancellationToken ct)
    {
        foreach (var other in await db.Vehicles.Where(v => v.IsDefault && v.Id != exceptId).ToListAsync(ct))
            other.IsDefault = false;
    }

    private static async Task<IResult> GetParameters(long vid, VehicleDataLoader loader, CancellationToken ct) =>
        Results.Ok(Mapping.ParseParameters((await loader.GetVehicleAsync(vid, ct)).ParametersJson));

    private static async Task<IResult> PutParameters(long vid, AnalysisParameters parameters, VehicleDataLoader loader,
        DriverBalanceDbContext db, CancellationToken ct)
    {
        var row = await loader.GetVehicleAsync(vid, ct);
        var errors = parameters.Validate();
        if (errors.Count > 0) throw new ApiValidationException(errors);
        row.ParametersJson = parameters.ToJson();
        await db.SaveChangesAsync(ct);
        return Results.Ok(parameters);
    }
}
