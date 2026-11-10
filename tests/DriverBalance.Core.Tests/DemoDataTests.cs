namespace DriverBalance.Core.Tests;

public class DemoDataTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static DemoCar Make() => DemoData.Generate(Today);

    [Fact]
    public void Fuel_expenses_have_station_with_town_and_no_place()
    {
        var fuel = Make().Expenses.Where(e => e.Category == ExpenseCategory.Fuel).ToList();
        Assert.NotEmpty(fuel);
        Assert.All(fuel, e =>
        {
            Assert.Null(e.Place);
            Assert.False(string.IsNullOrEmpty(e.Station));
            Assert.Contains(' ', e.Station!);
        });
    }

    [Fact]
    public void Same_today_gives_same_data()
    {
        var a = Make();
        var b = Make();
        Assert.Equal(a.Trips.Count, b.Trips.Count);
        Assert.Equal(a.Trips[0], b.Trips[0]);
        Assert.Equal(a.Trips[^1], b.Trips[^1]);
        Assert.Equal(a.Expenses.Sum(e => e.Amount), b.Expenses.Sum(e => e.Amount));
    }

    [Fact]
    public void Purchase_is_42_months_back_and_nothing_after_today()
    {
        var car = Make();
        Assert.Equal(new DateOnly(2023, 5, 10), car.PurchaseDate);
        Assert.Equal(car.PurchaseDate, car.HistoryStart);
        Assert.All(car.Trips, t => Assert.True(DateOnly.FromDateTime(t.End) <= Today));
        Assert.All(car.Expenses, e => Assert.True(e.Date <= Today));
        Assert.True(DateOnly.FromDateTime(car.Trips[0].Start) >= car.PurchaseDate);
    }

    [Theory]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 8, 31)]
    public void Month_end_and_leap_days_do_not_throw(int y, int m, int d)
    {
        var today = new DateOnly(y, m, d);
        var car = DemoData.Generate(today);
        Assert.Equal(today.AddMonths(-42), car.PurchaseDate);
        Assert.NotEmpty(car.Trips);
        Assert.All(car.Trips, t => Assert.True(DateOnly.FromDateTime(t.End) <= today));
        Assert.All(car.Expenses, e => Assert.True(e.Date <= today));
    }

    [Fact]
    public void Trips_are_ordered_continuous_and_do_not_overlap()
    {
        var trips = Make().Trips;
        for (var i = 0; i < trips.Count; i++)
        {
            var t = trips[i];
            Assert.Equal(t.End - t.Start, t.Duration);
            Assert.True(t.End > t.Start);
            Assert.True(Math.Abs(t.EndOdometer - t.StartOdometer - Math.Round(t.DistanceKm)) <= 1);
            if (i == 0) continue;
            var p = trips[i - 1];
            Assert.True(t.Start >= p.End, $"trip {i} starts {t.Start} before previous ends {p.End}");
            Assert.Equal(p.EndOdometer, t.StartOdometer);
        }
    }

    [Fact]
    public void Yearly_distance_and_consumption_are_realistic()
    {
        var car = Make();
        var km = car.Trips.Sum(t => t.DistanceKm);
        var days = Today.DayNumber - car.PurchaseDate.DayNumber;
        var perYear = km / (days / 365.25);
        Assert.InRange(perYear, 15000, 19000);
        var l100 = car.Trips.Sum(t => t.FuelLiters) / km * 100;
        Assert.InRange(l100, 6.0, 7.0);
        Assert.InRange(car.Trips.Count, 3500, 5500);
        Assert.All(car.Trips, t =>
        {
            Assert.True(t.DistanceKm > 0);
            Assert.InRange(t.FuelLiters / t.DistanceKm * 100, 3.5, 14);
        });
    }

    [Fact]
    public void Refuels_match_fuel_burned()
    {
        var car = Make();
        var fuel = car.Expenses.Where(e => e.Category == ExpenseCategory.Fuel).ToList();
        Assert.All(fuel, e =>
        {
            Assert.NotNull(e.Odometer);
            Assert.True(e.FullTank);
            Assert.InRange(e.Litres!.Value, 10.01, 59.0);
        });
        var burned = car.Trips.Sum(t => t.FuelLiters);
        var bought = fuel.Sum(e => e.Litres!.Value);
        Assert.True(Math.Abs(bought - burned) <= 0.03 * burned + 60, $"bought {bought} burned {burned}");
    }

    [Fact]
    public void Services_every_year_or_25000_km()
    {
        var car = Make();
        var services = car.Expenses
            .Where(e => e.Category == ExpenseCategory.Maintenance && e.Flags.HasFlag(MaintenanceType.OilChange))
            .OrderBy(e => e.Date).ToList();
        Assert.True(services.Count >= 3);
        var prevDate = car.PurchaseDate;
        var prevOdo = car.HistoryStartOdometer;
        foreach (var s in services)
        {
            Assert.True(s.Date <= prevDate.AddMonths(12).AddDays(15), $"service {s.Date} too long after {prevDate}");
            Assert.True(s.Odometer!.Value - prevOdo <= 26000);
            prevDate = s.Date;
            prevOdo = s.Odometer!.Value;
        }
    }

    [Fact]
    public void Tyre_swaps_in_spring_and_autumn()
    {
        var car = Make();
        var swaps = car.Expenses.Where(e => e.Flags.HasFlag(MaintenanceType.TyreSwap)).ToList();
        Assert.True(swaps.Count >= 4);
        var keys = car.TyreSets.Select(s => s.Key).ToHashSet();
        Assert.All(swaps, e =>
        {
            Assert.Contains(e.Date.Month, new[] { 3, 4, 5, 10, 11 });
            Assert.NotNull(e.TyreSetKey);
            Assert.Contains(e.TyreSetKey!, keys);
        });
        Assert.Contains(car.InitialTyreSetKey, keys);
        Assert.Single(car.Expenses, e => e.Flags.HasFlag(MaintenanceType.NewTyres));
    }

    [Fact]
    public void Every_category_is_used()
    {
        var used = Make().Expenses.Select(e => e.Category).ToHashSet();
        foreach (var c in new[]
        {
            ExpenseCategory.Fuel, ExpenseCategory.Parking, ExpenseCategory.TollsVignette, ExpenseCategory.Wash,
            ExpenseCategory.Maintenance, ExpenseCategory.Tyres, ExpenseCategory.Insurance, ExpenseCategory.Other,
            ExpenseCategory.Purchase,
        })
            Assert.Contains(c, used);
    }

    [Fact]
    public void Holidays_and_christmas_drives_exist()
    {
        var trips = Make().Trips;
        Assert.True(trips.Count(t => t.DistanceKm > 250 && t.Start.Month is 7 or 8) >= 3);
        Assert.True(trips.Count(t => t.DistanceKm > 250 && t.Start.Month == 12) >= 2);
    }

    [Fact]
    public void Expense_odometers_never_decrease_in_list_order()
    {
        var withOdo = Make().Expenses.Where(e => e.Odometer is not null).ToList();
        for (var i = 1; i < withOdo.Count; i++)
            Assert.True(withOdo[i].Odometer >= withOdo[i - 1].Odometer,
                $"{withOdo[i].Date} {withOdo[i].Note} odo {withOdo[i].Odometer} after {withOdo[i - 1].Odometer}");
    }

    [Fact]
    public void Every_trip_starts_where_the_previous_one_ended()
    {
        var trips = Make().Trips;
        for (var i = 1; i < trips.Count; i++)
            Assert.True(trips[i].StartAddress == trips[i - 1].EndAddress,
                $"trip {i} ({trips[i].Start}) starts at '{trips[i].StartAddress}' but previous ended at '{trips[i - 1].EndAddress}'");
    }

    public static IEnumerable<object[]> ManyToday()
    {
        for (var i = 0; i < 24; i++)
            yield return [new DateOnly(2026, 3, 1).AddDays(i * 45 + i % 3 * 7)];
        foreach (var d in new[] { new DateOnly(2026, 3, 31), new DateOnly(2026, 11, 30), new DateOnly(2027, 2, 28),
                     new DateOnly(2028, 2, 29), new DateOnly(2028, 8, 31), new DateOnly(2029, 2, 28) })
            yield return [d];
    }

    [Theory]
    [MemberData(nameof(ManyToday))]
    public void Scheduled_work_exists_for_any_creation_day(DateOnly today)
    {
        var car = DemoData.Generate(today);
        Assert.Single(car.Expenses, e => e.Flags.HasFlag(MaintenanceType.NewTyres));
        Assert.Contains(car.Expenses, e => e.Work?.Any(w => w.Name == "HU/AU (TÜV)") == true);
        Assert.Contains(car.Expenses, e => e.Work?.Any(w => w.Type == MaintenanceType.BrakePads) == true);
        Assert.Contains(car.Expenses, e => e.Work?.Any(w => w.Type == MaintenanceType.BrakeFluid) == true);
        Assert.All(car.Expenses, e => Assert.True(e.Date <= today));
        Assert.All(car.Trips, t => Assert.True(DateOnly.FromDateTime(t.End) <= today));

        var prevDate = car.PurchaseDate;
        var prevOdo = car.HistoryStartOdometer;
        foreach (var s in car.Expenses.Where(e => e.Flags.HasFlag(MaintenanceType.OilChange)).OrderBy(e => e.Date))
        {
            Assert.True(s.Date <= prevDate.AddMonths(12).AddDays(15), $"{today}: service {s.Date} too long after {prevDate}");
            Assert.True(s.Odometer!.Value - prevOdo <= 26000);
            prevDate = s.Date;
            prevOdo = s.Odometer!.Value;
        }
    }
}
