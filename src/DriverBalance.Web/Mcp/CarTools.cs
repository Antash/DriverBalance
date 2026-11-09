using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DriverBalance.Core;
using DriverBalance.Data;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DriverBalance.Web;

/// <summary>
/// MCP tools for logging and reading car data. Scoped services come from the request (the token's user), so every
/// query and write goes through the same user-filtered DbContext as the REST API.
/// </summary>
[McpServerToolType]
public sealed class CarTools(DriverBalanceDbContext db, VehicleDataLoader loader, AnalysisSessionFactory sessions, ImportService import, DocumentStore documents)
{
    private const string VehicleIdDoc = "Vehicle id. Optional: defaults to the user's default vehicle, else the first vehicle.";
    private const string DateDoc = "Date as yyyy-MM-dd. Optional: defaults to today in the vehicle's time zone.";
    private const string MaintenanceNames = "OilChange, BrakeFluid, TyreSwap, BrakePads, TransmissionOil, HaldexOil, Coolant, CabinFilter, AirFilter, FuelFilter, ACCleaning, NewTyres, OilFilter, BrakeDiscs, WheelAlignment, TimingBelt, AuxiliaryBelt, GlowPlugs, DpfService, Battery, WiperBladesFront, WiperBladesRear, Suspension, IntercoolerCleaning";

    private static readonly string CategoryNames = string.Join(", ", Enum.GetNames<ExpenseCategory>());

    [McpServerTool(Name = "add_expense"), Description(
        "Record any car expense. Returns the stored expense plus a one-line summary and needsConversion. " +
        "If the currency is not the vehicle's home currency and neither homeAmount nor rate is given, the expense is still saved " +
        "and needsConversion is true (it is excluded from totals until a home amount is set).")]
    public Task<CallToolResult> AddExpense(
        [Description("Category, one of: Fuel, Parking, TollsVignette (road tolls, vignette, road tax), Wash, Maintenance, Repair, Tyres, Accessories, Insurance, Fines, Other.")] string category,
        [Description("Amount paid, greater than 0, in `currency`.")] decimal amount,
        [Description("3-letter currency code. Optional: \"home\" (the default) means the vehicle's home currency.")] string currency = "home",
        [Description(DateDoc)] string? date = null,
        [Description("Odometer in km. Optional.")] int? odometer = null,
        [Description("Free-text note. Optional.")] string? note = null,
        [Description("Amount in the home currency. Optional; only needed for a foreign currency (wins over rate).")] decimal? homeAmount = null,
        [Description("Exchange rate to the home currency. Optional; homeAmount = amount * rate when homeAmount is not given.")] decimal? rate = null,
        [Description("For period costs such as insurance: first covered day, yyyy-MM-dd. Optional.")] string? coversFrom = null,
        [Description("For period costs such as insurance: last covered day, yyyy-MM-dd. Optional.")] string? coversTo = null,
        [Description(VehicleIdDoc)] long? vehicleId = null,
        CancellationToken ct = default) => Run(async () =>
    {
        var v = await ResolveVehicleAsync(vehicleId, ct);
        var cat = ParseCategory(category);
        var cur = ResolveCurrency(currency, v);
        var home = homeAmount;
        if (home is null && rate is { } r)
        {
            if (r <= 0) throw new ApiValidationException("rate must be greater than 0.");
            home = Math.Round(amount * r, 2, MidpointRounding.AwayFromZero);
        }
        return await SaveAsync(v, new Expense
        {
            Date = ParseDate(date, "date") ?? loader.TodayFor(v), Category = cat, Amount = amount, Currency = cur, HomeAmount = home,
            Odometer = odometer, Note = note, CoversFrom = ParseDate(coversFrom, "coversFrom"), CoversTo = ParseDate(coversTo, "coversTo"),
        }, ct);
    });

    [McpServerTool(Name = "log_fuel"), Description(
        "Record a fuel purchase. Returns the stored expense plus a one-line summary. " +
        "Optional: fullTank (default true), odometer, date (default today), station, currency (default the vehicle's home currency).")]
    public Task<CallToolResult> LogFuel(
        [Description("Litres filled, greater than 0.")] double litres,
        [Description("Total price paid, greater than 0.")] decimal totalPrice,
        [Description("True if the tank was filled completely (default true). Needed for full-tank consumption.")] bool fullTank = true,
        [Description("Odometer in km. Optional.")] int? odometer = null,
        [Description(DateDoc)] string? date = null,
        [Description("Station name. Optional.")] string? station = null,
        [Description("3-letter currency code. Optional: defaults to the vehicle's home currency. A foreign currency is saved with needsConversion = true.")] string? currency = null,
        [Description(VehicleIdDoc)] long? vehicleId = null,
        CancellationToken ct = default) => Run(async () =>
    {
        var v = await ResolveVehicleAsync(vehicleId, ct);
        return await SaveAsync(v, new Expense
        {
            Date = ParseDate(date, "date") ?? loader.TodayFor(v), Category = ExpenseCategory.Fuel, Amount = totalPrice,
            Currency = ResolveCurrency(currency, v), Litres = litres, FullTank = fullTank, Odometer = odometer, Station = station,
        }, ct);
    });

    [McpServerTool(Name = "log_maintenance"), Description(
        "Record a service or maintenance job. Returns the stored expense plus a one-line summary. " +
        "Optional: odometer, date (default today), description. Cost is in the vehicle's home currency.")]
    public Task<CallToolResult> LogMaintenance(
        [Description("What was done, any of: " + MaintenanceNames + ", Custom. An empty list means Custom.")] string[] types,
        [Description("Total cost, 0 or more (0 for free or unknown-cost work).")] decimal cost,
        [Description("Odometer in km. Optional.")] int? odometer = null,
        [Description(DateDoc)] string? date = null,
        [Description("Free-text description. Optional.")] string? description = null,
        [Description("Where the work was done (workshop or garage name), max 100 characters. Optional.")] string? place = null,
        [Description(VehicleIdDoc)] long? vehicleId = null,
        CancellationToken ct = default) => Run(async () =>
    {
        var v = await ResolveVehicleAsync(vehicleId, ct);
        var flags = MaintenanceType.Custom;
        foreach (var name in types ?? [])
            flags |= ParseMaintenanceType(name);
        return await SaveAsync(v, new Expense
        {
            Date = ParseDate(date, "date") ?? loader.TodayFor(v), Category = ExpenseCategory.Maintenance, Amount = cost,
            Currency = v.HomeCurrency, Maintenance = flags, MaintenanceDescription = description, Odometer = odometer, Place = place,
        }, ct);
    });

    [McpServerTool(Name = "swap_tyres"), Description(
        "Record mounting a tyre set (a tyre swap). Returns the stored expense plus a one-line summary. " +
        "Optional: odometer, date (default today), cost (default 0, in the vehicle's home currency). Use list_tyre_sets for ids.")]
    public Task<CallToolResult> SwapTyres(
        [Description("Id of the tyre set being mounted (from list_tyre_sets).")] long tyreSetId,
        [Description("Odometer in km. Optional.")] int? odometer = null,
        [Description(DateDoc)] string? date = null,
        [Description("Cost of the swap, 0 or more. Optional, default 0.")] decimal? cost = null,
        [Description(VehicleIdDoc)] long? vehicleId = null,
        CancellationToken ct = default) => Run(async () =>
    {
        var v = await ResolveVehicleAsync(vehicleId, ct);
        return await SaveAsync(v, new Expense
        {
            Date = ParseDate(date, "date") ?? loader.TodayFor(v), Category = ExpenseCategory.Maintenance, Amount = cost ?? 0,
            Currency = v.HomeCurrency, Maintenance = MaintenanceType.TyreSwap, TyreSetId = tyreSetId, Odometer = odometer,
        }, ct);
    });

    [McpServerTool(Name = "list_tyre_sets", ReadOnly = true), Description("List the vehicle's tyre sets (id, name, season, retired). Optional: vehicleId.")]
    public Task<CallToolResult> ListTyreSets(
        [Description(VehicleIdDoc)] long? vehicleId = null,
        CancellationToken ct = default) => Run(async () =>
    {
        var v = await ResolveVehicleAsync(vehicleId, ct);
        var sets = (await db.TyreSets.AsNoTracking().Where(t => t.VehicleId == v.Id).OrderBy(t => t.Id).ToListAsync(ct)).Select(t => t.ToDomain()).ToList();
        return Ok(new { tyreSets = sets }, $"{sets.Count} tyre set(s) on {v.Name}.");
    });

    [McpServerTool(Name = "list_recent_expenses", ReadOnly = true), Description("List the most recent expenses, newest first. Optional: count (default 10, max 100), vehicleId.")]
    public Task<CallToolResult> ListRecentExpenses(
        [Description("How many expenses to return. Optional, default 10, between 1 and 100.")] int count = 10,
        [Description(VehicleIdDoc)] long? vehicleId = null,
        CancellationToken ct = default) => Run(async () =>
    {
        var v = await ResolveVehicleAsync(vehicleId, ct);
        var take = Math.Clamp(count, 1, 100);
        var rows = await db.Expenses.AsNoTracking().Where(e => e.VehicleId == v.Id)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).Take(take).ToListAsync(ct);
        var docs = await DocumentEndpoints.LoadAsync(db, v.Id, rows.Select(r => r.Id).ToList(), ct);
        return Ok(new { expenses = rows.Select(r => r.ToDomain() with { Documents = docs.GetValueOrDefault(r.Id) ?? [] }).ToList() }, $"{rows.Count} most recent expense(s) on {v.Name}.");
    });

    [McpServerTool(Name = "delete_expense", Destructive = true), Description("Delete an expense by id; its attached documents are deleted too. Returns the deleted expense plus a one-line summary. Optional: vehicleId.")]
    public Task<CallToolResult> DeleteExpense(
        [Description("Id of the expense to delete (from list_recent_expenses).")] long id,
        [Description(VehicleIdDoc)] long? vehicleId = null,
        CancellationToken ct = default) => Run(async () =>
    {
        var v = await ResolveVehicleAsync(vehicleId, ct);
        var row = await db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.VehicleId == v.Id, ct)
            ?? throw new ApiValidationException($"Expense {id} not found on vehicle {v.Id}.");
        var docs = await DocumentEndpoints.LoadAsync(db, v.Id, [id], ct);
        var deleted = row.ToDomain() with { Documents = docs.GetValueOrDefault(id) ?? [] };
        await ExpenseDeletion.DeleteAsync(v.Id, id, db, documents, ct);
        return Ok(deleted, string.Create(CultureInfo.InvariantCulture,
            $"Deleted {deleted.Category} {deleted.Amount} {deleted.Currency} dated {deleted.Date:yyyy-MM-dd} (id {id})")
            + (deleted.Documents.Count > 0 ? $" and {deleted.Documents.Count} document(s)." : "."));
    });

    [McpServerTool(Name = "get_stats", ReadOnly = true), Description(
        "Driving, fuel and cost statistics for one period, computed with the vehicle's saved analysis parameters (returned as `parameters`). " +
        "kind \"interval\": a service interval for maintenanceType (e.g. OilChange); intervalIndex 0 = current, 1 = the one before, and so on. " +
        "kind \"range\": from (inclusive) and to (exclusive), yyyy-MM-dd; either may be omitted for all data. " +
        "kind \"calendar\": year (default this year) and optionally month 1-12 (omit for the whole year).")]
    public Task<CallToolResult> GetStats(
        [Description("\"interval\", \"range\" or \"calendar\".")] string kind,
        [Description("For interval: one of " + MaintenanceNames + ".")] string? maintenanceType = null,
        [Description("For interval: 0 = current interval (default), 1 = previous, ...")] int intervalIndex = 0,
        [Description("For range: first day (inclusive), yyyy-MM-dd. Optional.")] string? from = null,
        [Description("For range: day after the last day (exclusive), yyyy-MM-dd. Optional.")] string? to = null,
        [Description("For calendar: year. Optional, default this year.")] int? year = null,
        [Description("For calendar: month 1-12. Optional; omit for the whole year.")] int? month = null,
        [Description(VehicleIdDoc)] long? vehicleId = null,
        CancellationToken ct = default) => Run(async () =>
    {
        var v = await ResolveVehicleAsync(vehicleId, ct);
        var s = await sessions.OpenAsync(v.Id, null, ct);

        Period period;
        switch (kind?.Trim().ToLowerInvariant())
        {
            case "interval":
            {
                if (string.IsNullOrWhiteSpace(maintenanceType)) throw new ApiValidationException("maintenanceType is required for kind \"interval\".");
                var type = ParseMaintenanceType(maintenanceType);
                var intervals = Intervals.Build(type, s.Data.Expenses, s.DataStart, s.Today, s.DataStartOdometer);
                if (intervalIndex < 0 || intervalIndex >= intervals.Count)
                    throw new ApiValidationException($"intervalIndex {intervalIndex} is out of range; {type} has {intervals.Count} interval(s) (0 to {intervals.Count - 1}).");
                period = intervals[intervalIndex];
                break;
            }
            case "range":
            {
                var f = ParseDate(from, "from");
                var t = ParseDate(to, "to");
                period = s.Range(f, t, f is not null && t is not null ? $"{f:yyyy-MM-dd}..{t:yyyy-MM-dd}" : "All time");
                break;
            }
            case "calendar":
            {
                int y = year ?? s.Today.Year;
                if (y is < 1 or > 9998) throw new ApiValidationException("year is out of range.");
                if (month is { } m && m is < 1 or > 12) throw new ApiValidationException("month must be 1-12.");
                period = month is { } mm ? CalendarPeriods.Month(y, mm) : CalendarPeriods.Year(y);
                break;
            }
            default:
                throw new ApiValidationException("kind must be one of: interval, range, calendar.");
        }

        var stats = s.Context.Stats(period);
        var summary = string.Create(CultureInfo.InvariantCulture,
            $"{period.Label} ({period.From:yyyy-MM-dd} to {period.To:yyyy-MM-dd}, end exclusive): {stats.Usage.TripCount} trips, {stats.Usage.TripKm:0.#} km, total cost {stats.Money.Total} {v.HomeCurrency}.");
        return Ok(new { stats, parameters = s.Parameters }, summary);
    });

    [McpServerTool(Name = "import_trips_csv"), Description(
        "Import a Volvo trip-export CSV (the full file text, as exported; non-breaking spaces and CRLF line endings are fine). " +
        "Already-imported trips are skipped. Returns the import result (imported, skipped, firstTrip, lastTrip, warnings) plus a one-line summary.")]
    public Task<CallToolResult> ImportTripsCsv(
        [Description("The CSV file contents.")] string csvText,
        [Description(VehicleIdDoc)] long? vehicleId = null,
        CancellationToken ct = default) => Run(async () =>
    {
        var v = await ResolveVehicleAsync(vehicleId, ct);
        if (string.IsNullOrWhiteSpace(csvText)) throw new ApiValidationException("csvText is empty.");
        var result = await import.ImportCsvAsync(v.Id, "mcp-import.csv", new StringReader(csvText), ct);
        return Ok(result, $"Imported {result.Imported} trip(s), skipped {result.Skipped} duplicate(s) into {v.Name}.");
    });

    // ---- helpers ----

    private async Task<VehicleRow> ResolveVehicleAsync(long? vehicleId, CancellationToken ct)
    {
        if (vehicleId is { } id) return await loader.GetVehicleAsync(id, ct);
        return await db.Vehicles.OrderByDescending(v => v.IsDefault).ThenBy(v => v.Id).FirstOrDefaultAsync(ct)
            ?? throw new ApiValidationException("No vehicle exists yet; create one in the DriverBalance web app first.");
    }

    private async Task<CallToolResult> SaveAsync(VehicleRow v, Expense e, CancellationToken ct)
    {
        var valid = await ExpenseRules.ValidateAsync(v.Id, e with { Id = 0, Source = ExpenseSource.Mcp }, db, ct);
        var row = valid.ToRow();
        row.VehicleId = v.Id;
        db.Expenses.Add(row);
        await db.SaveChangesAsync(ct);

        var saved = row.ToDomain();
        bool needsConversion = Money.HomeAmountOf(saved, v.HomeCurrency) is null;
        var summary = string.Create(CultureInfo.InvariantCulture,
            $"Saved {saved.Category} {saved.Amount} {saved.Currency} dated {saved.Date:yyyy-MM-dd} on {v.Name} (id {saved.Id}).")
            + (needsConversion ? $" No {v.HomeCurrency} amount yet: needsConversion is true, so it is left out of totals until one is set." : "");
        return Ok(saved, summary, ("needsConversion", needsConversion));
    }

    /// <summary>JSON of the entity (same options as the REST API) with a `summary` line, plus any extra top-level fields.</summary>
    private static CallToolResult Ok(object payload, string summary, params (string Name, object? Value)[] extra)
    {
        var node = JsonSerializer.SerializeToNode(payload, ApiJson.Options)!.AsObject();
        node["summary"] = summary;
        foreach (var (name, value) in extra) node[name] = JsonSerializer.SerializeToNode(value, ApiJson.Options);
        return new CallToolResult { Content = [new TextContentBlock { Text = node.ToJsonString(ApiJson.Options) }] };
    }

    private static CallToolResult Error(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    /// <summary>Domain failures become readable MCP error results, never a 500.</summary>
    private static async Task<CallToolResult> Run(Func<Task<CallToolResult>> body)
    {
        try
        {
            return await body();
        }
        catch (ApiValidationException e)
        {
            return Error(string.Join(" ", e.Errors));
        }
        catch (VehicleNotFoundException e)
        {
            return Error(e.Message);
        }
    }

    private static ExpenseCategory ParseCategory(string? category) =>
        Enum.TryParse<ExpenseCategory>(category?.Trim(), ignoreCase: true, out var c) && Enum.IsDefined(c)
            ? c
            : throw new ApiValidationException($"Unknown category '{category}'. Valid categories: {CategoryNames}.");

    private static MaintenanceType ParseMaintenanceType(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        return Enum.GetNames<MaintenanceType>().Contains(trimmed, StringComparer.OrdinalIgnoreCase)
            ? Enum.Parse<MaintenanceType>(trimmed, ignoreCase: true)
            : throw new ApiValidationException($"Unknown maintenance type '{name}'. Valid types: {MaintenanceNames}, Custom.");
    }

    private static string ResolveCurrency(string? currency, VehicleRow v) =>
        string.IsNullOrWhiteSpace(currency) || currency.Trim().Equals("home", StringComparison.OrdinalIgnoreCase) ? v.HomeCurrency : currency.Trim();

    private static DateOnly? ParseDate(string? text, string field)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new ApiValidationException($"{field} must be a date as yyyy-MM-dd, got '{text}'.");
    }
}
