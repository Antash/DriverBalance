namespace DriverBalance.Core;

public sealed record FullTankSegment(DateOnly EndDate, int StartOdometer, int EndOdometer, double Litres, bool Inferred);

public static class FuelEconomy
{
    public static IReadOnlyList<FullTankSegment> Segments(IEnumerable<Expense> fuelExpenses, OdometerTimeline t)
    {
        var fills = new List<(DateOnly Date, int Odo, bool Inferred, double Litres, bool Full)>();
        foreach (var e in fuelExpenses)
        {
            int? odo = e.Odometer is > 0 ? e.Odometer : null;
            bool inferred = odo is null;
            odo ??= t.AtEndOfDay(e.Date);
            if (odo is null) continue;
            fills.Add((e.Date, odo.Value, inferred, e.Litres ?? 0, e.FullTank == true));
        }

        var result = new List<FullTankSegment>();
        (DateOnly Date, int Odo, bool Inferred, double Litres, bool Full)? anchor = null;
        double litres = 0;
        foreach (var f in fills.OrderBy(f => f.Odo).ThenBy(f => f.Date))
        {
            litres += f.Litres;
            if (!f.Full) continue;
            if (anchor is { } a && f.Odo > a.Odo)
                result.Add(new FullTankSegment(f.Date, a.Odo, f.Odo, litres, a.Inferred || f.Inferred));
            anchor = f;
            litres = 0;
        }
        return result;
    }
}
