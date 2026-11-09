namespace DriverBalance.Core;

public sealed record FullTankSegment(DateOnly EndDate, int StartOdometer, int EndOdometer, double Litres, bool Inferred);

public static class FuelEconomy
{
    public static IReadOnlyList<FullTankSegment> Segments(IEnumerable<Expense> fuelExpenses, OdometerTimeline t)
    {
        var fills = new List<(DateOnly Date, int Odo, bool Inferred, double? Litres, bool Full)>();
        foreach (var e in fuelExpenses)
        {
            int? odo = e.Odometer is > 0 ? e.Odometer : null;
            bool inferred = odo is null;
            odo ??= t.AtEndOfDay(e.Date, ignoreHistoryStart: true);
            if (odo is null) continue;
            fills.Add((e.Date, odo.Value, inferred, e.Litres, e.FullTank == true));
        }

        var result = new List<FullTankSegment>();
        (DateOnly Date, int Odo, bool Inferred, double? Litres, bool Full)? anchor = null;
        double litres = 0;
        bool missingLitres = false;
        foreach (var f in fills.OrderBy(f => f.Odo).ThenBy(f => f.Date))
        {
            if (f.Litres is { } l) litres += l; else missingLitres = true;
            if (!f.Full) continue;
            if (anchor is { } a && f.Odo > a.Odo && !missingLitres)
                result.Add(new FullTankSegment(f.Date, a.Odo, f.Odo, litres, a.Inferred || f.Inferred));
            anchor = f;
            litres = 0;
            missingLitres = false;
        }
        return result;
    }
}
