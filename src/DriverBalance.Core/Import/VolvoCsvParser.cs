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

        var header = reader.ReadLine() ?? ""; // may carry a BOM
        // English exports separate columns with ',', some locales (e.g. Russian) with ';'.
        char separator = header.Count(c => c == ';') > header.Count(c => c == ',') ? ';' : ',';
        int lineNo = 1;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = SplitCsvLine(line, separator);
            if (parts.Count < ColumnCount)
            {
                warnings.Add($"Line {lineNo}: expected {ColumnCount} columns but found {parts.Count}; row skipped.");
                continue;
            }
            try
            {
                trips.Add(FixClockChange(new Trip(
                    Category: parts[0],
                    Start: DateTime.Parse(parts[1], Culture),
                    StartOdometer: ParseInt(parts[2]),
                    StartAddress: parts[3],
                    End: DateTime.Parse(parts[4], Culture),
                    EndOdometer: ParseInt(parts[5]),
                    EndAddress: parts[6],
                    Duration: ParseDuration(parts[7]),
                    DistanceKm: ParseDouble(parts[8]),
                    FuelLiters: ParseDouble(parts[9]),
                    Notes: parts[10]), lineNo, warnings));
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

    /// <summary>
    /// Volvo computes the duration as stop − start on the car clock. When the clock falls back an hour
    /// (summer → winter time) during a trip, the start is on the old clock and the stop on the new one,
    /// so the duration comes out negative ("00:-41 h" for a 19-minute drive). A negative duration of at most
    /// one hour is that case: add the hour and move the start onto the new clock (end − duration).
    /// Anything more negative is bad data: keep the trip with no driving time.
    /// </summary>
    private static Trip FixClockChange(Trip trip, int lineNo, List<string> warnings)
    {
        if (trip.Duration >= TimeSpan.Zero) return trip;
        var hour = TimeSpan.FromHours(1);
        if (trip.Duration >= -hour)
        {
            var real = trip.Duration + hour;
            warnings.Add($"Line {lineNo}: clock change during the trip; duration corrected to {(int)real.TotalMinutes} min and start moved to {trip.End - real:HH:mm}.");
            return trip with { Duration = real, Start = trip.End - real };
        }
        warnings.Add($"Line {lineNo}: negative duration of {(int)trip.Duration.TotalMinutes} min; driving time set to 0.");
        return trip with { Duration = TimeSpan.Zero };
    }

    private static TimeSpan ParseDuration(string s)
    {
        s = s.Trim();
        // Newer exports write clock style "HH:MM h"; the sign (on either part) applies to the whole value.
        if (s.Contains(':'))
        {
            var clock = s.Replace("h", "").Trim().Split(':');
            if (clock.Length != 2) throw new FormatException($"Unknown duration '{s}'.");
            int h = int.Parse(clock[0].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            int m = int.Parse(clock[1].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            bool negative = h < 0 || m < 0 || clock[0].Trim().StartsWith('-');
            long minutes = checked(Math.Abs((long)h) * 60 + Math.Abs((long)m));
            return TimeSpan.FromMinutes(negative ? -minutes : minutes);
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

    private static List<string> SplitCsvLine(string line, char separator)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == separator && !inQuotes)
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
