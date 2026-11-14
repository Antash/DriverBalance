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
    private const double Eps = 1e-9;
    private const double MaxMagnitude = 1e15;

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
    private readonly int _today; // day number of Today
    private readonly int _poolFirst, _poolLast; // day numbers; pool windows are clipped to this range
    // Prefix sums: p[i] = sum of days [0, i).
    // All-data fuel totals (not clipped to the index): the last-resort price.
    private readonly double _allFuelSpend, _allLitreSpend, _allLitres, _allTripLitres;
    private readonly double[] _timePool, _wearPool, _fuelSpend, _litreSpend, _litres, _km, _hours, _tripLitres;

    private TripCosts(VehicleData data)
    {
        _currency = data.HomeCurrency;
        _today = data.Today.DayNumber;
        var counted = data.Expenses
            .Where(e => e.Category != ExpenseCategory.Purchase && Money.HomeAmountOf(e, data.HomeCurrency) is not null)
            .ToList();

        // The index covers the trip days plus the widest window on each side; anything outside can never reach a trip's window.
        int min = int.MaxValue, max = int.MinValue;
        foreach (var t in data.Trips)
        {
            if (!IsPlausible(t)) continue;
            int d = DayOf(t);
            min = Math.Min(min, d);
            max = Math.Max(max, d);
        }
        if (min > max) { min = max = _today; }
        // Prepaid cover reaching past the last trip has no driving to share it, so pool windows end at the later of the last trip and Today.
        _poolFirst = min;
        _poolLast = Math.Max(max, _today);
        _origin = min - PoolHalfWindowDays;
        _days = max - min + 2 * PoolHalfWindowDays + 1;

        Daily time = new(_days), wear = new(_days), fuel = new(_days), litreSpend = new(_days), litres = new(_days);
        foreach (var e in counted)
        {
            double amount = (double)Money.HomeAmountOf(e, data.HomeCurrency)!.Value;
            double? lit = e.Category == ExpenseCategory.Fuel && e.Litres is > 0 ? e.Litres : null;
            if (e.Category == ExpenseCategory.Fuel)
            {
                Spread(fuel, e, amount);
                _allFuelSpend += amount;
                if (lit is { } l) { Spread(litreSpend, e, amount); Spread(litres, e, l); _allLitreSpend += amount; _allLitres += l; }
            }
            else if (TimePoolCategories.Contains(e.Category)) Spread(time, e, amount);
            else if (WearPoolCategories.Contains(e.Category)) Spread(wear, e, amount);
        }

        var km = new double[_days];
        var hours = new double[_days];
        var tripLitres = new double[_days];
        foreach (var t in data.Trips)
        {
            if (!IsPlausible(t)) continue;
            int i = DayOf(t) - _origin;
            km[i] += t.DistanceKm;
            hours[i] += t.Duration.TotalHours;
            tripLitres[i] += t.FuelLiters;
            _allTripLitres += t.FuelLiters;
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

    private static int DayOf(Trip t) => DateOnly.FromDateTime(t.Start).DayNumber;

    /// <summary>Trips dated more than 100 years back or more than a year ahead are bogus: left out of every sum.</summary>
    private bool IsPlausible(Trip t)
    {
        int d = DayOf(t);
        return d >= _today - 36525 && d <= _today + 366;
    }

    /// <summary>Never throws: non-finite becomes 0 and magnitudes are clamped to +-1e15.</summary>
    private static decimal ToDecimal(double v)
    {
        if (!double.IsFinite(v)) return 0m;
        return (decimal)Math.Clamp(v, -MaxMagnitude, MaxMagnitude);
    }

    private static bool HasCoverage(Expense e) => e.CoversFrom is { } f && e.CoversTo is { } t && t >= f;

    /// <summary>Adds a value (same pro rata rule as <see cref="Money"/>): to one day, or evenly over the covered days via a difference array.</summary>
    private void Spread(Daily daily, Expense e, double value)
    {
        if (HasCoverage(e))
        {
            int from = e.CoversFrom!.Value.DayNumber - _origin, to = e.CoversTo!.Value.DayNumber - _origin;
            double perDay = value / ((long)to - from + 1); // full span, so the pro rata matches Money
            from = Math.Max(from, 0);
            to = Math.Min(to, _days - 1);
            if (from > to) return;
            daily.Diff[from] += perDay;
            daily.Diff[to + 1] -= perDay;
        }
        else
        {
            int i = e.Date.DayNumber - _origin;
            if (i >= 0 && i < _days) daily.Point[i] += value;
        }
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

    private double Window(double[] prefix, int centre, int half, bool pool = false)
    {
        int lo = Math.Max(centre - half - _origin, 0);
        int hi = Math.Min(centre + half - _origin, _days - 1);
        if (pool)
        {
            lo = Math.Max(lo, _poolFirst - _origin);
            hi = Math.Min(hi, _poolLast - _origin);
        }
        return hi < lo ? 0 : prefix[hi + 1] - prefix[lo];
    }

    private static double? Price(double litreSpend, double litres, double fuelSpend, double tripLitres)
    {
        if (litres > Eps) return litreSpend / litres;
        if (fuelSpend > Eps && tripLitres > Eps) return fuelSpend / tripLitres;
        return null;
    }

    private (double? Fuel, double Time, double Wear, double? Price) Raw(Trip trip)
    {
        if (!IsPlausible(trip)) return (0, 0, 0, null);
        int day = DayOf(trip);
        double hours = trip.Duration.TotalHours, km = trip.DistanceKm;

        double poolHours = Window(_hours, day, PoolHalfWindowDays, true), poolKm = Window(_km, day, PoolHalfWindowDays, true);
        double timeRate = poolHours > Eps ? Window(_timePool, day, PoolHalfWindowDays, true) / poolHours : 0;
        double wearPool = Window(_wearPool, day, PoolHalfWindowDays, true);
        double wearPerKm = poolKm > Eps ? wearPool / poolKm : 0;
        double wearPerHour = poolHours > Eps ? wearPool / poolHours : 0;

        double? price = Price(
            Window(_litreSpend, day, FuelHalfWindowDays), Window(_litres, day, FuelHalfWindowDays),
            Window(_fuelSpend, day, FuelHalfWindowDays), Window(_tripLitres, day, FuelHalfWindowDays))
            ?? Price(_allLitreSpend, _allLitres, _allFuelSpend, _allTripLitres);

        double? fuel = trip.FuelLiters <= Eps ? 0 : price is { } p ? trip.FuelLiters * p : null;
        return (fuel,
            hours * timeRate,
            0.5 * km * wearPerKm + 0.5 * hours * wearPerHour,
            price);
    }

    private TripCost Make(double? fuel, double time, double wear, double? price)
    {
        decimal? f = fuel is { } x ? Money.Round(ToDecimal(x)) : null;
        return new TripCost(f, Money.Round(ToDecimal(time)), Money.Round(ToDecimal(wear)),
            Money.Round(ToDecimal((fuel ?? 0) + time + wear)),
            price is { } p ? Money.Round(ToDecimal(p)) : null, _currency);
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
