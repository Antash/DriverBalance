namespace DriverBalance.Core;

public static class PlacesAnalysis
{
    public static IReadOnlyList<PlaceStat> Places(AnalysisContext c, Period p, int top = 15)
    {
        var sp = new AddressSpellings();
        var starts = new Dictionary<string, int>();
        var ends = new Dictionary<string, int>();
        foreach (var g in c.GroupsIn(p))
        {
            if (!string.IsNullOrWhiteSpace(g.Trips[0].StartAddress))
            {
                var k = sp.Add(g.Trips[0].StartAddress);
                starts[k] = starts.GetValueOrDefault(k) + 1;
            }
            if (!string.IsNullOrWhiteSpace(g.Trips[^1].EndAddress))
            {
                var k = sp.Add(g.Trips[^1].EndAddress);
                ends[k] = ends.GetValueOrDefault(k) + 1;
            }
        }
        return starts.Keys.Union(ends.Keys)
            .Select(k => new PlaceStat(sp.Display(k), starts.GetValueOrDefault(k), ends.GetValueOrDefault(k)))
            .OrderByDescending(x => x.Starts + x.Ends).ThenBy(x => x.Address, StringComparer.Ordinal)
            .Take(top).ToList();
    }

    public static IReadOnlyList<RouteStat> Routes(AnalysisContext c, Period p, int top = 15)
    {
        var sp = new AddressSpellings();
        var byRoute = new Dictionary<(string From, string To), List<TripGroup>>();
        foreach (var g in c.GroupsIn(p))
        {
            string from = g.Trips[0].StartAddress, to = g.Trips[^1].EndAddress;
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) continue;
            var key = (sp.Add(from), sp.Add(to));
            if (!byRoute.TryGetValue(key, out var list)) byRoute[key] = list = new List<TripGroup>();
            list.Add(g);
        }
        return byRoute
            .Select(kv =>
            {
                var gs = kv.Value;
                double km = gs.Sum(g => g.DistanceKm);
                double liters = gs.Sum(g => g.FuelLiters);
                var typical = gs.GroupBy(g => g.Score).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).First().Key;
                var trend = gs.OrderBy(g => g.Start).Select(g => new RouteTrendPoint(g.StartDate, g.LPer100)).ToList();
                return new RouteStat(sp.Display(kv.Key.From), sp.Display(kv.Key.To), gs.Count, km,
                    km > 0 ? liters / km * 100.0 : null, typical, trend);
            })
            .OrderByDescending(r => r.Count).ThenByDescending(r => r.Km)
            .ThenBy(r => r.From, StringComparer.Ordinal).ThenBy(r => r.To, StringComparer.Ordinal)
            .Take(top).ToList();
    }
}
