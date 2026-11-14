namespace DriverBalance.Core;

/// <summary>Approximate cost of a trip in the home currency; <see cref="Fuel"/> and <see cref="PricePerLitre"/> are null when no fuel price is known.</summary>
public sealed record TripCost(decimal? Fuel, decimal Time, decimal Wear, decimal Total, decimal? PricePerLitre, string Currency);

/// <summary>
/// Trip cost = fuel (litres x local average price) + share of the time pool + share of the wear pool.
/// Per-day totals are built once as prefix sums, so every window is O(1).
/// </summary>
public sealed class TripCosts
{
    private const int FuelHalfWindowDays = 45;
    private const int PoolHalfWindowDays = 182;

    private static readonly HashSet<ExpenseCategory> TimePoolCategories =
    [
        ExpenseCategory.Insurance, ExpenseCategory.TollsVignette, ExpenseCategory.Parking, ExpenseCategory.Wash,
        ExpenseCategory.Accessories, ExpenseCategory.Fines, ExpenseCategory.Other,
    ];

    private static readonly HashSet<ExpenseCategory> WearPoolCategories =
    [
        ExpenseCategory.Maintenance, ExpenseCategory.Repair, ExpenseCategory.Tyres,
    ];

    private readonly string _currency;
    private readonly int _origin; // day number of index 0
    private readonly int _days;
    // Prefix sums: p[i] = sum of days [0, i).
    private readonly double[] _timePool, _wearPool, _fuelSpend, _litreSpend, _litres, _km, _hours, _tripLitres;

    private TripCosts(VehicleData data)
    {
        _currency = data.HomeCurrency;
        var counted = data.Expenses
            .Where(e => e.Category != ExpenseCategory.Purchase && Money.HomeAmountOf(e, data.HomeCurrency) is not null)
            .ToList();

        int min = int.MaxValue, max = int.MinValue;
        void Touch(DateOnly d) { min = Math.Min(min, d.DayNumber); max = Math.Max(max, d.DayNumber); }
        foreach (var t in data.Trips) Touch(DateOnly.FromDateTime(t.Start));
        foreach (var e in counted)
        {
            Touch(e.Date);
            if (HasCoverage(e)) { Touch(e.CoversFrom!.Value); Touch(e.CoversTo!.Value); }
        }
        if (min > max) { min = max = 0; }
        _origin = min;
        _days = max - min + 1;

        Daily time = new(_days), wear = new(_days), fuel = new(_days), litreSpend = new(_days), litres = new(_days);
        foreach (var e in counted)
        {
            double amount = (double)Money.HomeAmountOf(e, data.HomeCurrency)!.Value;
            double? lit = e.Category == ExpenseCategory.Fuel && e.Litres is > 0 ? e.Litres : null;
            if (e.Category == ExpenseCategory.Fuel)
            {
                Spread(fuel, e, amount);
                if (lit is { } l) { Spread(litreSpend, e, amount); Spread(litres, e, l); }
            }
            else if (TimePoolCategories.Contains(e.Category)) Spread(time, e, amount);
            else if (WearPoolCategories.Contains(e.Category)) Spread(wear, e, amount);
        }

        var km = new double[_days];
        var hours = new double[_days];
        var tripLitres = new double[_days];
        foreach (var t in data.Trips)
        {
            int i = DateOnly.FromDateTime(t.Start).DayNumber - _origin;
            km[i] += t.DistanceKm;
            hours[i] += t.Duration.TotalHours;
            tripLitres[i] += t.FuelLiters;
        }

        _timePool = Prefix(time.Resolve());
        _wearPool = Prefix(wear.Resolve());
        _fuelSpend = Prefix(fuel.Resolve());
        _litreSpend = Prefix(litreSpend.Resolve());
        _litres = Prefix(litres.Resolve());
        _km = Prefix(km);
        _hours = Prefix(hours);
        _tripLitres = Prefix(tripLitres);
    }

    public static TripCosts Build(VehicleData data) => new(data);

    private static bool HasCoverage(Expense e) => e.CoversFrom is { } f && e.CoversTo is { } t && t >= f;

    /// <summary>Adds a value (same pro rata rule as <see cref="Money"/>): to one day, or evenly over the covered days via a difference array.</summary>
    private void Spread(Daily daily, Expense e, double value)
    {
        if (HasCoverage(e))
        {
            int from = e.CoversFrom!.Value.DayNumber - _origin, to = e.CoversTo!.Value.DayNumber - _origin;
            double perDay = value / (to - from + 1);
            daily.Diff[from] += perDay;
            daily.Diff[to + 1] -= perDay;
        }
        else daily.Point[e.Date.DayNumber - _origin] += value;
    }

    private sealed class Daily(int length)
    {
        public readonly double[] Point = new double[length];
        public readonly double[] Diff = new double[length + 1];

        /// <summary>Per-day values: point amounts plus the running sum of the difference array.</summary>
        public double[] Resolve()
        {
            var r = new double[Point.Length];
            double run = 0;
            for (int i = 0; i < r.Length; i++) { run += Diff[i]; r[i] = Point[i] + run; }
            return r;
        }
    }

    private static double[] Prefix(double[] daily)
    {
        var p = new double[daily.Length + 1];
        for (int i = 1; i < p.Length; i++) p[i] = p[i - 1] + daily[i - 1];
        return p;
    }

    private double Window(double[] prefix, int centre, int half)
    {
        int lo = Math.Max(centre - half - _origin, 0);
        int hi = Math.Min(centre + half - _origin, _days - 1);
        return hi < lo ? 0 : prefix[hi + 1] - prefix[lo];
    }

    private double All(double[] prefix) => prefix[_days];

    private static double? Price(double litreSpend, double litres, double fuelSpend, double tripLitres)
    {
        if (litres > 0) return litreSpend / litres;
        if (fuelSpend > 0 && tripLitres > 0) return fuelSpend / tripLitres;
        return null;
    }

    private (double? Fuel, double Time, double Wear, double? Price) Raw(Trip trip)
    {
        int day = DateOnly.FromDateTime(trip.Start).DayNumber;
        double hours = trip.Duration.TotalHours, km = trip.DistanceKm;

        double poolHours = Window(_hours, day, PoolHalfWindowDays), poolKm = Window(_km, day, PoolHalfWindowDays);
        double timeRate = poolHours > 0 ? Window(_timePool, day, PoolHalfWindowDays) / poolHours : 0;
        double wearPool = Window(_wearPool, day, PoolHalfWindowDays);
        double wearPerKm = poolKm > 0 ? wearPool / poolKm : 0;
        double wearPerHour = poolHours > 0 ? wearPool / poolHours : 0;

        double? price = Price(
            Window(_litreSpend, day, FuelHalfWindowDays), Window(_litres, day, FuelHalfWindowDays),
            Window(_fuelSpend, day, FuelHalfWindowDays), Window(_tripLitres, day, FuelHalfWindowDays))
            ?? Price(All(_litreSpend), All(_litres), All(_fuelSpend), All(_tripLitres));

        return (price is { } p ? trip.FuelLiters * p : null,
            hours * timeRate,
            0.5 * km * wearPerKm + 0.5 * hours * wearPerHour,
            price);
    }

    private TripCost Make(double? fuel, double time, double wear, double? price)
    {
        decimal? f = fuel is { } x ? Money.Round((decimal)x) : null;
        return new TripCost(f, Money.Round((decimal)time), Money.Round((decimal)wear),
            Money.Round((decimal)((fuel ?? 0) + time + wear)),
            price is { } p ? Money.Round((decimal)p) : null, _currency);
    }

    public TripCost Of(Trip trip)
    {
        var r = Raw(trip);
        return Make(r.Fuel, r.Time, r.Wear, r.Price);
    }

    public TripCost Of(TripGroup group)
    {
        double? fuel = 0;
        double time = 0, wear = 0;
        foreach (var t in group.Trips)
        {
            var r = Raw(t);
            fuel = fuel is { } f && r.Fuel is { } rf ? f + rf : null;
            time += r.Time;
            wear += r.Wear;
        }
        return Make(fuel, time, wear, null);
    }
}
