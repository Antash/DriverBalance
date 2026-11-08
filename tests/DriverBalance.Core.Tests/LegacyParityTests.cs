using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DriverBalance.Core.Tests;

/// <summary>
/// Compares the new analysis against the stdout captured from the legacy console
/// (tests/fixtures/legacy/legacy-output.txt). Expected values are parsed from that file.
/// </summary>
public class LegacyParityTests
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly AnalysisParameters P = new();

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static AnalysisContext LoadContext()
    {
        using var reader = new StreamReader(FixturePath("synthetic-trips.csv"));
        var trips = VolvoCsvParser.Parse(reader).Trips;
        Assert.Equal(119, trips.Count);
        return AnalysisContext.Create(
            new VehicleData(trips, Array.Empty<Expense>(), Array.Empty<TyreSet>(), "CZK", new DateOnly(2026, 11, 8)), P);
    }

    private sealed record MaintEvent(DateOnly Date, MaintenanceType Type);

    // Test-local reader for the legacy maintenance JSON (a real LegacyJsonReader comes in a later task).
    private static List<MaintEvent> LoadMaintenance()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(FixturePath("synthetic-maintenance.json")));
        return doc.RootElement.EnumerateArray()
            .Select(e => new MaintEvent(
                DateOnly.FromDateTime(e.GetProperty("Date").GetDateTime()),
                (MaintenanceType)e.GetProperty("Type").GetInt32()))
            .ToList();
    }

    private static string[] LegacyLines() => File.ReadAllLines(FixturePath("legacy-output.txt"));

    private static string Group1(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.Multiline);
        Assert.True(m.Success, $"pattern not found in legacy output: {pattern}");
        return m.Groups[1].Value;
    }

    private static Period Everything => new(new DateOnly(2000, 1, 1), new DateOnly(2100, 1, 1), "all");

    [Fact]
    public void Summary_MatchesLegacy()
    {
        var all = string.Join("\n", LegacyLines());
        var start = all.IndexOf("--- Trip Summary ---", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = all.IndexOf("--- Trip Record Manager ---", start, StringComparison.Ordinal);
        var summary = all[start..end];

        var s = LoadContext().Stats(Everything);

        Assert.Equal(int.Parse(Group1(summary, @"^Total cold starts: (\d+)"), Inv), s.Usage.ColdStarts);
        Assert.Equal(Group1(summary, @"^Total distance: (\d+\.\d+) km"), s.Usage.TripKm.ToString("F2", Us));
        Assert.Equal(Group1(summary, @"^Total fuel burned: (\d+\.\d+) L"), s.Fuel.TripLiters.ToString("F2", Us));
        Assert.Equal(Group1(summary, @"^Average speed: (\d+\.\d+) km/h"), s.Usage.AverageSpeedKmh!.Value.ToString("F2", Us));
        Assert.Equal(Group1(summary, @"^Fuel consumption: (\d+\.\d+) L/100km"), s.Fuel.TripLPer100!.Value.ToString("F2", Us));
        foreach (var score in Enum.GetValues<DrivingScore>())
            Assert.Equal(Group1(summary, $@"^{score}: (\d+\.\d+) km"), s.Scores.Buckets[score].Km.ToString("F1", Us));
        Assert.Equal(Group1(summary, @"^DPF index: (\d+\.\d+%)"), s.Scores.DpfIndex!.Value.ToString("P2", Us));
    }

    [Fact]
    public void GroupAnalysis_MatchesLegacy()
    {
        const string fmt = "M/d/yyyy h:mm:ss tt";
        var rx = new Regex(@"^(\d+/\d+/\d{4} \d+:\d+:\d+ [AP]M) - (\d+/\d+/\d{4} \d+:\d+:\d+ [AP]M): (\d+\.\d) km, .*Score: (\w+)$");
        var expected = LegacyLines().Select(l => rx.Match(l)).Where(m => m.Success).ToList();
        var groups = LoadContext().Groups;

        Assert.NotEmpty(expected);
        Assert.Equal(expected.Count, groups.Count);
        for (int i = 0; i < groups.Count; i++)
        {
            var m = expected[i];
            Assert.Equal(DateTime.ParseExact(m.Groups[1].Value, fmt, Us), groups[i].Start);
            Assert.Equal(DateTime.ParseExact(m.Groups[2].Value, fmt, Us), groups[i].End);
            Assert.Equal(m.Groups[3].Value, groups[i].DistanceKm.ToString("F1", Us));
            Assert.Equal(m.Groups[4].Value, groups[i].Score.ToString());
        }
    }

    private sealed record LegacyPeriod(MaintenanceType Type, DateOnly From, DateOnly To, Dictionary<string, string> Values);

    private static List<LegacyPeriod> ParseLegacyPeriods()
    {
        var lines = LegacyLines();
        int idx = Array.FindIndex(lines, l => l.Contains("Trip Report by All Maintenance Periods"));
        Assert.True(idx >= 0);

        var result = new List<LegacyPeriod>();
        MaintenanceType? type = null;
        LegacyPeriod? current = null;
        var header = new Regex(@"^(\w+):$");
        var period = new Regex(@"^\s+Period: (\d{4}-\d{2}-\d{2}) to (\d{4}-\d{2}-\d{2})$");
        var metric = new Regex(@"^\s+([A-Za-z ]+): (.*)$");
        for (int i = idx + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var pm = period.Match(line);
            if (pm.Success)
            {
                current = new LegacyPeriod(type!.Value,
                    DateOnly.ParseExact(pm.Groups[1].Value, "yyyy-MM-dd", Inv),
                    DateOnly.ParseExact(pm.Groups[2].Value, "yyyy-MM-dd", Inv),
                    new Dictionary<string, string>());
                result.Add(current);
                continue;
            }
            var hm = header.Match(line);
            if (hm.Success && Enum.TryParse<MaintenanceType>(hm.Groups[1].Value, out var t))
            {
                type = t;
                current = null;
                continue;
            }
            var mm = metric.Match(line);
            if (mm.Success && current != null)
                current.Values[mm.Groups[1].Value.Trim()] = mm.Groups[2].Value;
        }
        return result;
    }

    [Fact]
    public void AllPeriods_MatchesLegacy()
    {
        var ctx = LoadContext();
        var events = LoadMaintenance();
        var legacy = ParseLegacyPeriods();
        Assert.NotEmpty(legacy);

        int compared = 0;
        foreach (var byType in legacy.GroupBy(l => l.Type))
        {
            var dates = events.Where(e => e.Type.HasFlag(byType.Key)).Select(e => e.Date).OrderBy(d => d).ToList();
            var blocks = byType.ToList();
            Assert.Equal(dates.Count, blocks.Count);
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                bool last = i + 1 == dates.Count;
                // The current interval's end comes from the run date in the legacy output; ignore it.
                var to = last ? new DateOnly(2100, 1, 1) : dates[i + 1];
                Assert.Equal(dates[i], block.From);
                if (!last) Assert.Equal(to, block.To);

                var s = ctx.Stats(new Period(dates[i], to, $"{byType.Key} {i}"));
                var v = block.Values;
                var where = $"{byType.Key} period {block.From}";

                Assert.True(int.Parse(v["Cold starts"], Inv) == s.Usage.ColdStarts, where + " cold starts");
                Assert.Equal(v["Total distance"], s.Usage.TripKm.ToString("F2", Us) + " km");
                Assert.Equal(v["Total fuel"], s.Fuel.TripLiters.ToString("F2", Us) + " L");
                Assert.Equal(v["Average speed"], s.Usage.AverageSpeedKmh!.Value.ToString("F2", Us) + " km/h");
                Assert.Equal(v["Fuel consumption"], s.Fuel.TripLPer100!.Value.ToString("F2", Us) + " L/100km");
                Assert.Equal(v["DPF index"], s.Scores.DpfIndex!.Value.ToString("P2", Us));
                foreach (var score in Enum.GetValues<DrivingScore>())
                    Assert.StartsWith(s.Scores.Buckets[score].Km.ToString("F1", Us) + " km", v[score.ToString()]);
                compared++;
            }
        }
        Assert.Equal(legacy.Count, compared);
    }
}
