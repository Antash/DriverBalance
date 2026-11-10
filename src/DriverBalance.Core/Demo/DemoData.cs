namespace DriverBalance.Core;

public sealed record DemoTyreSet(string Key, string Name, string Brand, string Model, string Size, TyreSeason Season, DateOnly? PurchaseDate, bool Retired);

public sealed record DemoExpense(DateOnly Date, ExpenseCategory Category, decimal Amount, int? Odometer, string? Note,
    DateOnly? CoversFrom, DateOnly? CoversTo, double? Litres, bool? FullTank, string? Station, string? Place,
    MaintenanceType Flags, IReadOnlyList<MaintenanceWork>? Work, string? TyreSetKey);

public sealed record DemoCar(string Name, string FuelType, string HomeCurrency, string TimeZone,
    DateOnly PurchaseDate, DateOnly HistoryStart, int HistoryStartOdometer, decimal EstimatedResaleValue, DateOnly ResaleEstimateDate,
    string Model, string Engine, string Trim, int ModelYear, string ExteriorColour, string Interior, IReadOnlyList<string> Equipment,
    string Transmission, bool Awd, string InitialTyreSetKey,
    IReadOnlyList<DemoTyreSet> TyreSets, IReadOnlyList<Trip> Trips, IReadOnlyList<DemoExpense> Expenses);

/// <summary>
/// A made-up but believable German BMW 320d with 42 months of trips and costs, generated day by day from the purchase date
/// up to <c>today</c>. Pure and deterministic: the same <c>today</c> and <c>seed</c> always give the same car.
/// </summary>
public static class DemoData
{
    public const int StartOdometer = 10;

    public static DemoCar Generate(DateOnly today, int seed = 2026)
    {
        var purchase = today.AddMonths(-42);
        var rng = new Random(seed);
        var expenses = new List<DemoExpense>();
        var sim = new DemoTripSim(rng, purchase, today, expenses);
        var costs = new DemoCosts(sim, rng, purchase, today, expenses);

        for (var d = purchase; d <= today; d = d.AddDays(1))
        {
            if (d < today) sim.SimulateDay(d);
            costs.OnDay(d);
        }

        return new DemoCar(
            Name: "Demo · BMW 320d Touring",
            FuelType: "DieselMhev",
            HomeCurrency: "EUR",
            TimeZone: "Europe/Berlin",
            PurchaseDate: purchase,
            HistoryStart: purchase,
            HistoryStartOdometer: StartOdometer,
            EstimatedResaleValue: 31000m,
            ResaleEstimateDate: today,
            Model: "BMW 320d Touring (G21)",
            Engine: "2.0 diesel 48V mild hybrid, 140 kW",
            Trim: "M Sport",
            ModelYear: purchase.Year,
            ExteriorColour: "Mineral White",
            Interior: "Black Vernasca leather",
            Equipment: ["Adaptive LED headlights", "Driving Assistant Professional", "Panoramic glass roof",
                "Harman Kardon sound system", "Head-up display", "Comfort Access", "Heated front seats"],
            Transmission: "Automatic",
            Awd: true,
            InitialTyreSetKey: DemoCosts.SummerKey,
            TyreSets: costs.TyreSets,
            Trips: sim.Trips,
            Expenses: expenses.OrderBy(e => e.Date).ThenBy(e => e.Odometer ?? 0).ToList());
    }
}
