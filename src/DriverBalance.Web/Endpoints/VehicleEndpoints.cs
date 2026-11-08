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

        v.MapGet("/{vid:long}/parameters", GetParameters);
        v.MapPut("/{vid:long}/parameters", PutParameters);
        api.MapGet("/parameters/defaults", () => Results.Ok(new AnalysisParameters()));
    }

    private static async Task<IResult> List(DriverBalanceDbContext db, CancellationToken ct) =>
        Results.Ok((await db.Vehicles.AsNoTracking().OrderBy(v => v.Id).ToListAsync(ct)).Select(VehicleDto.From).ToList());

    private static async Task<IResult> Get(long vid, VehicleDataLoader loader, CancellationToken ct) =>
        Results.Ok(VehicleDto.From(await loader.GetVehicleAsync(vid, ct)));

    private static async Task<IResult> Create(VehicleInput input, DriverBalanceDbContext db, CancellationToken ct)
    {
        var errors = Validate(input);
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
        var errors = Validate(input);
        if (input.InitialTyreSetId is { } setId && !await db.TyreSets.AnyAsync(t => t.Id == setId && t.VehicleId == vid, ct))
            errors.Add("Initial tyre set does not exist on this vehicle.");
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

    private static List<string> Validate(VehicleInput input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name)) errors.Add("Name is required.");
        if (input.HomeCurrency is { } cur && !IsCurrency(cur)) errors.Add("Home currency must be a 3-letter code.");
        if (input.TimeZone is { } tz && !VehicleDataLoader.TimeZoneExists(tz)) errors.Add($"Unknown time zone '{tz}'.");
        return errors;
    }

    /// <summary>Name is required (validated); currency and time zone keep their current or default value when omitted.</summary>
    private static void Apply(VehicleInput input, VehicleRow row)
    {
        row.Name = input.Name!.Trim();
        if (input.HomeCurrency is { } cur) row.HomeCurrency = cur.ToUpperInvariant();
        if (input.TimeZone is { } tz) row.TimeZone = tz;
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
