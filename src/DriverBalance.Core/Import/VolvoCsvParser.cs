using System.Globalization;
using System.Text;

namespace DriverBalance.Core;

public sealed record ParseResult(IReadOnlyList<Trip> Trips, IReadOnlyList<string> Warnings);

/// <summary>Parses the Volvo trip export (ported from the legacy TripImporter).</summary>
public static class VolvoCsvParser
{
    private const int ColumnCount = 11;
    private static readonly CultureInfo Culture = new("cs-CZ");

    public static ParseResult Parse(TextReader reader)
    {
        var trips = new List<Trip>();
        var warnings = new List<string>();

        reader.ReadLine(); // header (may carry a BOM)
        int lineNo = 1;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = SplitCsvLine(line);
            if (parts.Count < ColumnCount)
            {
                warnings.Add($"Line {lineNo}: expected {ColumnCount} columns but found {parts.Count}; row skipped.");
                continue;
            }
            try
            {
                trips.Add(new Trip(
                    Category: parts[0],
                    Start: DateTime.Parse(parts[1], Culture),
                    StartOdometer: ParseInt(parts[2]),
                    StartAddress: parts[3],
                    End: DateTime.Parse(parts[4], Culture),
                    EndOdometer: ParseInt(parts[5]),
                    EndAddress: parts[6],
                    Duration: ParseDuration(parts[7], lineNo, warnings),
                    DistanceKm: ParseDouble(parts[8]),
                    FuelLiters: ParseDouble(parts[9]),
                    Notes: parts[10]));
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentOutOfRangeException)
            {
                warnings.Add($"Line {lineNo}: {ex.Message} Row skipped.");
            }
        }
        return new ParseResult(trips, warnings);
    }

    private static string CleanNumber(string s) => s.Replace(" ", "").Replace(" ", "");

    private static int ParseInt(string s) => int.TryParse(CleanNumber(s), out var v) ? v : 0;

    private static double ParseDouble(string s) =>
        ToFinite(CleanNumber(s).Replace(',', '.'));

    private static double ToFinite(string s)
    {
        if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return 0.0;
        return double.IsFinite(v) ? v : throw new FormatException($"Non-finite number '{s}'.");
    }

    private static TimeSpan ParseDuration(string s, int lineNo, List<string> warnings)
    {
        s = s.Trim();
        // Newer exports write "HH:MM h" (clock style); Volvo sometimes emits a negative minute part ("00:-41 h")
        // when the stop time is before the start time. Keep the trip, count no driving time.
        if (s.Contains(':'))
        {
            var clock = s.Replace("h", "").Trim().Split(':');
            if (clock.Length != 2) throw new FormatException($"Unknown duration '{s}'.");
            int h = int.Parse(clock[0].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            int m = int.Parse(clock[1].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            var span = TimeSpan.FromMinutes(checked(h * 60L + m));
            if (span < TimeSpan.Zero)
            {
                warnings.Add($"Line {lineNo}: negative duration '{s}'; driving time set to 0.");
                return TimeSpan.Zero;
            }
            return span;
        }
        if (s.Contains('h'))
        {
            int hours = 0, minutes = 0;
            foreach (var part in s.Split(','))
            {
                if (part.Contains('h')) hours = int.Parse(part.Replace("h", "").Trim(), CultureInfo.InvariantCulture);
                if (part.Contains("min")) minutes = int.Parse(part.Replace("min", "").Trim(), CultureInfo.InvariantCulture);
            }
            return new TimeSpan(hours, minutes, 0);
        }
        if (s.Contains("min"))
            return new TimeSpan(0, int.Parse(s.Replace("min", "").Trim(), CultureInfo.InvariantCulture), 0);
        return TimeSpan.Zero;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }
}
