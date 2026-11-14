using System.Security.Cryptography;
using System.Text;

namespace DriverBalance.Core;

public sealed record RegularRoute(string Key, string From, string To, IReadOnlyList<string> Via, bool RoundTrip, int Count,
    string Typical, double AvgKm, decimal AvgCost, decimal PerMonth, double Share, string Currency);

/// <summary>Routes driven often enough to be "regular", found automatically from trip (or group) start and end addresses.</summary>
public static class RegularRoutes
{
    public const int MinDrives = 5;
    private const char Sep = '\u001f';

    public static IReadOnlyList<TripGroup> Items(AnalysisContext c, Period p, TrendLevel level)
    {
        if (level == TrendLevel.Group) return c.GroupsIn(p).ToList();
        return c.Groups.SelectMany(g => g.Trips)
            .Where(t => p.Contains(DateOnly.FromDateTime(t.Start)))
            .Select(t => new TripGroup([t], Scoring.Score(t.Duration, t.AverageSpeedKmh, c.Parameters)))
            .ToList();
    }

    /// <summary>Normalized endpoints and stops of an item in drive direction, or null when an endpoint address is blank.</summary>
    private static (string First, string Last, List<string> Stops)? Normal(TripGroup item)
    {
        var trips = item.Trips;
        if (trips.Count == 0 || string.IsNullOrWhiteSpace(trips[0].StartAddress) || string.IsNullOrWhiteSpace(trips[^1].EndAddress)) return null;
        string first = AddressSpellings.Normalize(trips[0].StartAddress), last = AddressSpellings.Normalize(trips[^1].EndAddress);
        var stops = new List<string>();
        for (int i = 0; i < trips.Count - 1; i++)
        {
            if (string.IsNullOrWhiteSpace(trips[i].EndAddress)) continue;
            var s = AddressSpellings.Normalize(trips[i].EndAddress);
            if (stops.Count == 0 || stops[^1] != s) stops.Add(s);
        }
        stops.RemoveAll(s => s == first || s == last);
        return (first, last, stops);
    }

    /// <summary>Canonical orientation: the direction whose normal form sorts first, so both directions give the same one.</summary>
    private static (string A, string B, List<string> Stops, string Form) Canonical((string First, string Last, List<string> Stops) n)
    {
        string forward = string.Join(Sep, new[] { n.First, n.Last }.Concat(n.Stops));
        var rev = Enumerable.Reverse(n.Stops).ToList();
        string backward = string.Join(Sep, new[] { n.Last, n.First }.Concat(rev));
        return string.CompareOrdinal(forward, backward) <= 0
            ? (n.First, n.Last, n.Stops, forward)
            : (n.Last, n.First, rev, backward);
    }

    private static string Hash(string form) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(form)))[..16].ToLowerInvariant();

    public static string? KeyOf(TripGroup item) => Normal(item) is { } n ? Hash(Canonical(n).Form) : null;

    public static IReadOnlyList<RegularRoute> Build(AnalysisContext c, Period p, TrendLevel level)
    {
        var items = Items(c, p, level);
        var sp = new AddressSpellings();
        var byKey = new Dictionary<string, List<(TripGroup Item, string First)>>();
        var shape = new Dictionary<string, (string A, string B, List<string> Stops)>();
        foreach (var item in items)
        {
            if (Normal(item) is not { } n) continue;
            var cn = Canonical(n);
            string key = Hash(cn.Form);
            sp.Add(item.Trips[0].StartAddress);
            sp.Add(item.Trips[^1].EndAddress);
            foreach (var t in item.Trips.Take(item.Trips.Count - 1))
                if (!string.IsNullOrWhiteSpace(t.EndAddress)) sp.Add(t.EndAddress);
            if (!byKey.TryGetValue(key, out var list)) byKey[key] = list = [];
            list.Add((item, n.First));
            shape[key] = (cn.A, cn.B, cn.Stops);
        }

        decimal allCost = items.Sum(i => c.Costs.Of(i).Total);
        decimal months = MonthsIn(c, p);

        return byKey
            .Where(kv => kv.Value.Count >= MinDrives)
            .Select(kv =>
            {
                var rows = kv.Value;
                var (a, b, stops) = shape[kv.Key];
                int startsA = rows.Count(r => r.First == a), startsB = rows.Count(r => r.First == b);
                // Display direction: the endpoint that is the start more often goes first; ties alphabetical (a sorts before b).
                bool aFirst = startsA >= startsB;
                string from = aFirst ? a : b, to = aFirst ? b : a;
                var via = (aFirst ? stops : Enumerable.Reverse(stops).ToList()).Select(sp.Display).ToList();
                var group = rows.Select(r => r.Item).ToList();
                decimal total = group.Sum(i => c.Costs.Of(i).Total);
                double km = group.Sum(i => i.DistanceKm);
                var route = new RegularRoute(kv.Key, sp.Display(from), sp.Display(to), via, a == b, group.Count,
                    Typical(group.Select(i => i.Start).ToList()), km / group.Count,
                    Math.Round(total / group.Count, 2), Math.Round(total / months, 2),
                    allCost > 0 ? (double)(total / allCost) : 0, c.Data.HomeCurrency);
                return (Route: route, Km: km);
            })
            .OrderByDescending(x => x.Route.Count).ThenByDescending(x => x.Km).ThenBy(x => x.Route.From, StringComparer.Ordinal)
            .Select(x => x.Route).ToList();
    }

    /// <summary>Months that count: later of period start and first trip to earlier of period end and today, inclusive, divided by 30.44.</summary>
    private static decimal MonthsIn(AnalysisContext c, Period p)
    {
        var first = c.Data.Trips.Count > 0 ? DateOnly.FromDateTime(c.Data.Trips.Min(t => t.Start)) : p.From;
        var from = first > p.From ? first : p.From;
        var lastDay = p.To.AddDays(-1);
        var to = c.Data.Today < lastDay ? c.Data.Today : lastDay;
        int days = to.DayNumber - from.DayNumber + 1;
        return days <= 0 ? 1m : days / 30.44m;
    }

    private static string Typical(IReadOnlyList<DateTime> starts)
    {
        int n = starts.Count;
        int weekdays = starts.Count(s => s.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        string day = weekdays >= 0.8 * n ? "Weekdays" : n - weekdays >= 0.8 * n ? "Weekends" : "Any day";
        var hours = starts.GroupBy(s => s.Hour).Where(g => g.Count() >= 0.2 * n)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(2).Select(g => g.Key).OrderBy(h => h)
            .Select(h => $"{h}–{h + 1}").ToList();
        return hours.Count == 0 ? day : $"{day} · {string.Join(", ", hours)}";
    }
}
