namespace DriverBalance.Core;

/// <summary>
/// Day-by-day driving simulation for the demo car: commutes, short trips, weekend outings, one summer holiday and one
/// Christmas drive a year. Keeps the odometer and the tank, and writes the fuel, parking, wash and AdBlue expenses that
/// follow from the driving.
/// </summary>
internal sealed class DemoTripSim
{
    public const double TankLiters = 59;

    private enum Kind { Short, Commute, Regional, Outing, Holiday }

    // speed km/h (lo, hi) and consumption L/100 km (lo, hi) per kind of trip
    private static (double SpeedLo, double SpeedHi, double LLo, double LHi) Profile(Kind k) => k switch
    {
        Kind.Short => (22, 32, 7.5, 9.8),
        Kind.Commute => (38, 50, 5.0, 5.8),
        Kind.Regional => (45, 60, 5.9, 6.8),
        Kind.Outing => (70, 85, 5.9, 6.7),
        _ => (100, 112, 6.1, 6.6),
    };

    private sealed record JourneyDay(DemoPlaces.Journey Journey, int Index);

    private readonly Random _rng;
    private readonly DateOnly _purchase;
    private readonly DateOnly _lastTripDay;
    private readonly List<DemoExpense> _expenses;
    private readonly HashSet<DateOnly> _publicHolidays = [];
    private readonly HashSet<DateOnly> _vacation = [];
    private readonly Dictionary<DateOnly, JourneyDay> _journeyDays = [];

    private double _tank = 55;
    private double _refuelAt;
    private double _nextAdBlueKm;
    private DateTime _cursor = DateTime.MinValue;
    private int _tripsToday;
    private string _country = "DE";
    private string? _town; // null = near home

    public int Odometer { get; private set; } = DemoData.StartOdometer;
    public List<Trip> Trips { get; } = [];

    public DemoTripSim(Random rng, DateOnly purchase, DateOnly today, List<DemoExpense> expenses)
    {
        _rng = rng;
        _purchase = purchase;
        _lastTripDay = today.AddDays(-1);
        _expenses = expenses;
        _refuelAt = NextRefuelThreshold();
        _nextAdBlueKm = 10000 + rng.Next(-500, 500);
        for (var y = purchase.Year; y <= today.Year; y++) PlanYear(y);
    }

    public bool IsPublicHoliday(DateOnly d) => _publicHolidays.Contains(d);
    public bool InTravelBlock(DateOnly d) => _journeyDays.ContainsKey(d);

    // ---- planning ----------------------------------------------------------------------------------------------

    private void PlanYear(int y)
    {
        var easter = Easter(y);
        foreach (var h in new[]
        {
            new DateOnly(y, 1, 1), new DateOnly(y, 1, 6), easter.AddDays(-2), easter.AddDays(1), new DateOnly(y, 5, 1),
            easter.AddDays(39), easter.AddDays(50), easter.AddDays(60), new DateOnly(y, 8, 15), new DateOnly(y, 10, 3),
            new DateOnly(y, 11, 1), new DateOnly(y, 12, 25), new DateOnly(y, 12, 26),
        }) _publicHolidays.Add(h);

        // Summer holiday: leave on a Saturday between 8 July and mid August, alternate Lake Garda and the Croatian coast.
        var summerStart = NextSaturday(new DateOnly(y, 7, 8).AddDays(_rng.Next(0, 31)));
        var summer = y % 2 == 1 ? DemoPlaces.Garda : DemoPlaces.Croatia;
        PlanJourney(summer, summerStart);
        for (var i = 9; i < 14; i++) _vacation.Add(summerStart.AddDays(i)); // a staycation week after coming back

        // Christmas: to family in Hamburg or Cologne and back.
        PlanJourney(y % 2 == 0 ? DemoPlaces.Hamburg : DemoPlaces.Cologne, new DateOnly(y, 12, 23));
        for (var d = new DateOnly(y, 12, 24); d <= new DateOnly(y, 12, 31); d = d.AddDays(1)) _vacation.Add(d);

        // A few long weekends and short breaks.
        for (var b = 0; b < 3; b++)
        {
            var month = new[] { 2, 3, 5, 6, 9, 10 }[_rng.Next(6)];
            var start = new DateOnly(y, month, _rng.Next(1, 25));
            for (var i = 0; i < _rng.Next(3, 6); i++) _vacation.Add(start.AddDays(i));
        }
    }

    private void PlanJourney(DemoPlaces.Journey j, DateOnly start)
    {
        var end = start.AddDays(j.Days - 1);
        if (start < _purchase.AddDays(7) || end > _lastTripDay) return;
        for (var i = 0; i < j.Days; i++)
        {
            _journeyDays[start.AddDays(i)] = new JourneyDay(j, i);
            _vacation.Add(start.AddDays(i));
        }
    }

    private static DateOnly NextSaturday(DateOnly d) => d.AddDays(((int)DayOfWeek.Saturday - (int)d.DayOfWeek + 7) % 7);

    private static DateOnly Easter(int y)
    {
        var a = y % 19; var b = y / 100; var c = y % 100; var d = b / 4; var e = b % 4;
        var f = (b + 8) / 25; var g = (b - f + 1) / 3; var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4; var k = c % 4; var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(y, month, day);
    }

    // ---- days --------------------------------------------------------------------------------------------------

    public void SimulateDay(DateOnly d)
    {
        _cursor = DateTime.MinValue;
        _tripsToday = 0;

        if (d == _purchase) AddTrip(d, T(15, 30), DemoPlaces.DealerAddress, DemoPlaces.Home, 14.2, Kind.Commute);
        else if (_journeyDays.TryGetValue(d, out var jd)) JourneyDayTrips(d, jd);
        else NormalDay(d);

        if (_tripsToday > 0) EndOfDay(d);
    }

    private static TimeSpan T(int h, int m) => new(h, m, 0);
    private TimeSpan Between(int fromH, int fromM, int minutes) => T(fromH, fromM) + TimeSpan.FromMinutes(_rng.Next(0, minutes + 1));
    private double U(double lo, double hi) => lo + (hi - lo) * _rng.NextDouble();

    private void NormalDay(DateOnly d)
    {
        var weekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var off = weekend || _publicHolidays.Contains(d) || _vacation.Contains(d);

        if (!off)
        {
            if (_rng.NextDouble() < 0.68) Commute(d);
            else
            {
                if (_rng.NextDouble() < 0.70) Pair(d, Between(10, 0, 180));
                if (_rng.NextDouble() < 0.65) Pair(d, Between(17, 30, 120));
            }
            return;
        }

        if (_rng.NextDouble() < (weekend ? 0.16 : 0.10)) { Outing(d); return; }
        var r = _rng.NextDouble();
        var pairs = d.DayOfWeek == DayOfWeek.Saturday
            ? (r < 0.10 ? 0 : r < 0.35 ? 1 : r < 0.70 ? 2 : 3)
            : (r < 0.25 ? 0 : r < 0.65 ? 1 : 2);
        var slots = new[] { Between(9, 0, 150), Between(14, 0, 180), Between(18, 30, 90) };
        for (var i = 0; i < pairs; i++) Pair(d, slots[i]);
    }

    private void Commute(DateOnly d)
    {
        var t = Between(7, 0, 90);
        var from = DemoPlaces.Home;
        if (_rng.NextDouble() < 0.30)
        {
            AddTrip(d, t, from, DemoPlaces.School, U(2.5, 4.0), Kind.Short);
            from = DemoPlaces.School;
            t = _cursor.TimeOfDay + TimeSpan.FromMinutes(_rng.Next(4, 10));
        }
        AddTrip(d, t, from, DemoPlaces.Work, U(24, 27), Kind.Commute);

        t = Between(16, 30, 120);
        if (_rng.NextDouble() < 0.28)
        {
            AddTrip(d, t, DemoPlaces.Work, DemoPlaces.Supermarket, U(6, 9), Kind.Short);
            AddTrip(d, _cursor.TimeOfDay + TimeSpan.FromMinutes(_rng.Next(20, 50)), DemoPlaces.Supermarket, DemoPlaces.Home, U(5, 8), Kind.Short);
        }
        else AddTrip(d, t, DemoPlaces.Work, DemoPlaces.Home, U(24, 27), Kind.Commute);

        if (_rng.NextDouble() < 0.65) Pair(d, Between(18, 45, 105));
    }

    /// <summary>A short errand: out, a stay, and back.</summary>
    private void Pair(DateOnly d, TimeSpan t, DemoPlaces.Poi? poi = null, string home = DemoPlaces.Home, Kind kind = Kind.Short, double? km = null)
    {
        poi ??= DemoPlaces.Short[_rng.Next(DemoPlaces.Short.Length)];
        var outKm = km ?? Math.Max(2.0, poi.Km * U(0.7, 1.0));
        if (!AddTrip(d, t, home, poi.Address, outKm, kind)) return;
        var back = _cursor.TimeOfDay + TimeSpan.FromMinutes(_rng.Next(15, 121));
        if (back > T(21, 40)) back = T(21, 40);
        AddTrip(d, back, poi.Address, home, outKm * U(0.92, 1.08), kind);
    }

    private void Outing(DateOnly d)
    {
        var dest = DemoPlaces.Outings[_rng.Next(DemoPlaces.Outings.Length)];
        var km = dest.Km * U(0.95, 1.06);
        if (!AddTrip(d, Between(8, 30, 120), DemoPlaces.Home, dest.Address, km, Kind.Outing)) return;
        AddTrip(d, _cursor.TimeOfDay + TimeSpan.FromMinutes(_rng.Next(180, 360)), dest.Address, DemoPlaces.Home, km * U(0.97, 1.05), Kind.Outing);
    }

    private void JourneyDayTrips(DateOnly d, JourneyDay jd)
    {
        var j = jd.Journey;
        var startOdo = Odometer;
        if (jd.Index == 0) Legs(d, j, forward: true);
        else if (jd.Index == j.Days - 1) Legs(d, j, forward: false);
        else
        {
            var dest = j.Stops[^1];
            _country = dest.Country; _town = dest.Town;
            var pairs = j.Summer ? (_rng.NextDouble() < 0.85 ? 2 : 1) : (_rng.NextDouble() < 0.6 ? 1 : 0);
            var slots = new[] { Between(9, 30, 120), Between(14, 30, 150) };
            for (var i = 0; i < pairs; i++)
                Pair(d, slots[i], new DemoPlaces.Poi(j.LocalPois[_rng.Next(j.LocalPois.Length)], U(6, 30)), dest.Address, Kind.Regional, U(6, 30));
        }

        if (jd.Index == 0 || jd.Index == j.Days - 1)
        {
            var driven = Odometer - startOdo;
            foreach (var toll in jd.Index == 0 ? j.Out : j.Back)
                _expenses.Add(Cost(d, ExpenseCategory.TollsVignette, TollAmount(toll, d), startOdo + (int)Math.Round(driven * toll.Fraction), toll.Note, place: toll.Place));
        }
        _country = "DE"; _town = null;
    }

    private static decimal TollAmount(DemoPlaces.Toll t, DateOnly d) =>
        Math.Round(t.BaseAmount * (1m + 0.04m * (Math.Clamp(d.Year, 2023, 2030) - 2023)) * 10m) / 10m;

    private void Legs(DateOnly d, DemoPlaces.Journey j, bool forward)
    {
        var t = Between(7, 30, 90);
        for (var i = 0; i < j.LegKm.Length; i++)
        {
            var from = forward ? j.Stops[i] : j.Stops[j.Stops.Length - 1 - i];
            var to = forward ? j.Stops[i + 1] : j.Stops[j.Stops.Length - 2 - i];
            var km = (forward ? j.LegKm[i] : j.LegKm[j.LegKm.Length - 1 - i]) * U(0.97, 1.04);
            _country = from.Country; _town = from.Town == "Unterhaching" ? null : from.Town;
            AddTrip(d, t, from.Address, to.Address, km, Kind.Holiday);
            t = _cursor.TimeOfDay + TimeSpan.FromMinutes(_rng.Next(20, 46));
        }
    }

    // ---- trips and fuel ------------------------------------------------------------------------------------------

    private bool AddTrip(DateOnly day, TimeSpan desired, string from, string to, double km, Kind kind)
    {
        km = Math.Round(km, 1);
        var p = Profile(kind);
        var start = day.ToDateTime(TimeOnly.MinValue) + desired;
        if (start < _cursor.AddMinutes(3)) start = _cursor.AddMinutes(3);
        start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0, DateTimeKind.Unspecified);
        var minutes = Math.Max(2, (int)Math.Round(km / U(p.SpeedLo, p.SpeedHi) * 60));
        var end = start.AddMinutes(minutes);
        if (start.Hour >= 22 || end.Date != start.Date) return false;

        var factor = start.Month switch
        {
            12 or 1 or 2 => U(1.08, 1.12),
            11 or 3 => U(1.03, 1.07),
            _ => 1.0,
        };
        var fuel = Math.Round(km * U(p.LLo, p.LHi) * factor * U(0.97, 1.03) / 100, 1);
        while (fuel / km * 100 > 13.5 && fuel > 0) fuel = Math.Round(fuel - 0.1, 1);
        while (fuel / km * 100 < 3.8) fuel = Math.Round(fuel + 0.1, 1);

        if (_tank < _refuelAt || _tank - fuel < 3.5 || (kind == Kind.Holiday && _tank < 0.7 * TankLiters))
            Refuel(day);

        _tank -= fuel;
        var odo = Odometer;
        Odometer += (int)Math.Round(km);
        Trips.Add(new Trip("Private", start, odo, from, end, Odometer, to, end - start, km, fuel, ""));
        _cursor = end;
        _tripsToday++;
        return true;
    }

    private double NextRefuelThreshold() => TankLiters * U(0.10, 0.20);

    private void Refuel(DateOnly day)
    {
        var litres = Math.Round((TankLiters - _tank) * U(0.99, 1.01), 1);
        litres = Math.Min(litres, 58.9);
        string brand, town;
        if (_town is null)
        {
            (brand, town) = DemoPlaces.HomeStations[_rng.Next(DemoPlaces.HomeStations.Length)];
            if (_country != "DE") brand = DemoPlaces.StationBrand(_country, _rng);
        }
        else { brand = DemoPlaces.StationBrand(_country, _rng); town = _town; }

        var price = DemoPlaces.DieselPrice(day, _country, _rng);
        _expenses.Add(new DemoExpense(day, ExpenseCategory.Fuel, Math.Round((decimal)litres * price, 2), Odometer, null,
            null, null, litres, true, brand, town, MaintenanceType.Custom, null, null));
        _tank = TankLiters;
        _refuelAt = NextRefuelThreshold();
    }

    private void EndOfDay(DateOnly d)
    {
        if (_rng.NextDouble() < 0.095)
            _expenses.Add(Cost(d, ExpenseCategory.Parking, Math.Round((decimal)U(2, 15) * 2m) / 2m, Odometer, "City parking", place: "München"));
        if (d.DayOfWeek == DayOfWeek.Saturday && _rng.NextDouble() < 0.23)
            _expenses.Add(Cost(d, ExpenseCategory.Wash, Math.Round((decimal)U(8, 18) * 2m) / 2m, Odometer, "Car wash", place: "Unterhaching"));
        if (Odometer >= _nextAdBlueKm)
        {
            _expenses.Add(Cost(d, ExpenseCategory.Other, Math.Round((decimal)U(15, 25) * 2m) / 2m, Odometer, "AdBlue top-up", place: "Unterhaching"));
            _nextAdBlueKm += 9000 + _rng.Next(0, 2000);
        }
    }

    internal static DemoExpense Cost(DateOnly date, ExpenseCategory category, decimal amount, int odometer, string note, string? place = null,
        MaintenanceType flags = MaintenanceType.Custom, IReadOnlyList<MaintenanceWork>? work = null, string? tyreSetKey = null,
        DateOnly? coversFrom = null, DateOnly? coversTo = null) =>
        new(date, category, amount, odometer, note, coversFrom, coversTo, null, null, null, place, flags, work, tyreSetKey);
}
