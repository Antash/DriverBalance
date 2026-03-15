using ScottPlot;
using System.Globalization;
using System.Security.Principal;
using System.Text.Json;

public class TripRecord
{
    public string Category { get; set; }
    public DateTime StartTime { get; set; }
    public int StartOdometer { get; set; }
    public string StartAddress { get; set; }
    public DateTime EndTime { get; set; }
    public int EndOdometer { get; set; }
    public string EndAddress { get; set; }
    public TimeSpan Duration { get; set; }
    public double DistanceKm { get; set; }
    public double FuelLiters { get; set; }
    public string UserNotes { get; set; }

    public double AverageSpeed => Duration.TotalHours > 0 ? DistanceKm / Duration.TotalHours : 0;

    public override bool Equals(object? obj)
    {
        if (obj is not TripRecord other) return false;
        return StartTime == other.StartTime && EndTime == other.EndTime;
    }
    public override int GetHashCode() => HashCode.Combine(StartTime, EndTime);
}

public interface ITripRepository
{
    List<TripRecord> GetAll();
    void AddRange(IEnumerable<TripRecord> records, out List<TripRecord> duplicates);
    void Save();
    void Load();
}

public class JsonTripRepository : ITripRepository
{
    private readonly string _filePath;
    private List<TripRecord> _records = new();
    public JsonTripRepository(string filePath) => _filePath = filePath;
    public List<TripRecord> GetAll() => _records;
    public void AddRange(IEnumerable<TripRecord> records, out List<TripRecord> duplicates)
    {
        duplicates = new List<TripRecord>();
        foreach (var rec in records)
        {
            if (_records.Contains(rec))
                duplicates.Add(rec);
            else
                _records.Add(rec);
        }
    }
    public void Save()
    {
        var json = JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
    public void Load()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            _records = JsonSerializer.Deserialize<List<TripRecord>>(json) ?? new List<TripRecord>();
        }
        else
        {
            _records = new List<TripRecord>();
        }
    }
}

public class TripImporter
{
    public IEnumerable<TripRecord> ImportFromCsv(string filePath)
    {
        var culture = new CultureInfo("cs-CZ");
        var result = new List<TripRecord>();
        using (var reader = new StreamReader(filePath))
        {
            string? header = reader.ReadLine(); // skip header
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = SplitCsvLine(line);
                if (parts.Count < 11) continue;
                result.Add(new TripRecord
                {
                    Category = parts[0],
                    StartTime = DateTime.Parse(parts[1], culture),
                    StartOdometer = ParseInt(parts[2]),
                    StartAddress = parts[3],
                    EndTime = DateTime.Parse(parts[4], culture),
                    EndOdometer = ParseInt(parts[5]),
                    EndAddress = parts[6],
                    Duration = ParseDuration(parts[7]),
                    DistanceKm = ParseDouble(parts[8]),
                    FuelLiters = ParseDouble(parts[9]),
                    UserNotes = parts[10]
                });
            }
        }
        return result;
    }
    static int ParseInt(string s)
    {
        s = s.Replace("\u00A0", "").Replace(" ", "");
        return int.TryParse(s, out var value) ? value : 0;
    }
    static double ParseDouble(string s)
    {
        s = s.Replace("\u00A0", "").Replace(" ", "");
        return double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0.0;
    }
    static TimeSpan ParseDuration(string s)
    {
        s = s.Trim();
        if (s.Contains("h"))
        {
            int hours = 0, minutes = 0;
            var parts = s.Split(',');
            foreach (var part in parts)
            {
                if (part.Contains("h"))
                    hours = int.Parse(part.Replace("h", "").Trim());
                if (part.Contains("min"))
                    minutes = int.Parse(part.Replace("min", "").Trim());
            }
            return new TimeSpan(hours, minutes, 0);
        }
        else if (s.Contains("min"))
        {
            int minutes = int.Parse(s.Replace("min", "").Trim());
            return new TimeSpan(0, minutes, 0);
        }
        else if (s.Contains("0 min"))
        {
            return TimeSpan.Zero;
        }
        return TimeSpan.Zero;
    }
    static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        string current = "";
        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }
        result.Add(current);
        return result;
    }
}

public class TripSummaryService
{
    public void PrintSummary(IEnumerable<TripRecord> records, TripAnalysisService analysisService, TripAnalysisParameters analysisParams)
    {
        double totalDistance = records.Sum(r => r.DistanceKm);
        double totalFuel = records.Sum(r => r.FuelLiters);
        TimeSpan totalTime = records.Aggregate(TimeSpan.Zero, (sum, r) => sum + r.Duration);
        double avgSpeed = totalTime.TotalHours > 0 ? totalDistance / totalTime.TotalHours : 0;
        double fuelPer100km = totalDistance > 0 ? (totalFuel / totalDistance) * 100.0 : 0;
        // DPF index calculation
        double goodKm = 0, allKm = 0;
        foreach (var group in TripAnalysisService.ConcatenateTrips(records.OrderBy(t => t.StartTime).ToList(), analysisParams.ColdStartThreshold))
        {
            var concatTrip = analysisService.ConcatenateGroup(group);
            var bestScore = analysisService.GetBestScoreForGroup(group);
            if (bestScore == DrivingScore.Excellent || bestScore == DrivingScore.Good)
                goodKm += concatTrip.DistanceKm;
            allKm += concatTrip.DistanceKm;
        }
        double dpf = allKm > 0 ? goodKm / allKm : 0;
        Console.WriteLine("--- Trip Summary ---");
        Console.WriteLine($"Total distance: {totalDistance:F2} km");
        Console.WriteLine($"Total time: {totalTime}");
        Console.WriteLine($"Total fuel burned: {totalFuel:F2} L");
        Console.WriteLine($"Average speed: {avgSpeed:F2} km/h");
        Console.WriteLine($"Fuel consumption: {fuelPer100km:F2} L/100km");
        Console.WriteLine($"DPF index: {dpf:P2} ({goodKm:F1} km of {allKm:F1} km)");
    }
}

public class TripAnalysisParameters
{
    public TimeSpan ColdStartThreshold { get; set; } = TimeSpan.FromMinutes(45);
    public TimeSpan ShortDuration { get; set; } = TimeSpan.FromMinutes(20);
    public TimeSpan MediumDuration { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan LongDuration { get; set; } = TimeSpan.FromMinutes(40);
    public double HeavyTrafficSpeed { get; set; } = 25.0;
    public double ModerateCitySpeed { get; set; } = 30.0;
    public double MixedSpeed { get; set; } = 40.0;
    public int DpfMovingAverageDays { get; set; } = 21;
    public int DpfMovingAverageWeeks { get; set; } = 4;
}

public enum DrivingScore { Excellent = 1, Good = 2, Fair = 3, Bad = 4 }

public class TripAnalysisService
{
    private readonly TripAnalysisParameters _params;
    public TripAnalysisService(TripAnalysisParameters parameters) => _params = parameters;

    public DrivingScore AnalyzeScore(TripRecord trip)
    {
        var duration = trip.Duration;
        var speed = trip.AverageSpeed;

        if (duration >= _params.LongDuration && speed >= _params.MixedSpeed)
            return DrivingScore.Excellent;
        if (duration >= _params.MediumDuration && speed >= _params.ModerateCitySpeed)
            return DrivingScore.Good;
        if (duration >= _params.ShortDuration && speed >= _params.HeavyTrafficSpeed)
            return DrivingScore.Fair;
        return DrivingScore.Bad;
    }

    public void AnalyzeAndPrint(IEnumerable<TripRecord> trips)
    {
        var groups = ConcatenateTrips(trips.OrderBy(t => t.StartTime).ToList(), _params.ColdStartThreshold);
        Console.WriteLine("--- Trip Analysis (Concatenated) ---");
        foreach (var group in groups)
        {
            var bestScore = GetBestScoreForGroup(group);
            var concatTrip = ConcatenateGroup(group);
            // Use sum of durations for driving time
            double drivingDurationHours = group.Sum(g => g.Duration.TotalHours);
            double totalDistance = group.Sum(g => g.DistanceKm);
            double totalFuel = group.Sum(g => g.FuelLiters);
            double avgSpeed = drivingDurationHours > 0 ? totalDistance / drivingDurationHours : 0;
            double fuelPer100km = totalDistance > 0 ? (totalFuel / totalDistance) * 100.0 : 0;
            TimeSpan totalDrivingDuration = TimeSpan.FromHours(drivingDurationHours);
            Console.WriteLine($"{concatTrip.StartTime} - {concatTrip.EndTime}: {totalDistance:F1} km, {totalDrivingDuration}, Avg speed: {avgSpeed:F1} km/h, Fuel: {fuelPer100km:F2} L/100km, Score: {bestScore}");
        }
    }

    public static List<List<TripRecord>> ConcatenateTrips(List<TripRecord> trips, TimeSpan threshold)
    {
        var result = new List<List<TripRecord>>();
        if (trips.Count == 0) return result;
        var current = new List<TripRecord> { trips[0] };
        for (int i = 1; i < trips.Count; i++)
        {
            var prev = current.Last();
            var next = trips[i];
            if (next.StartTime - prev.EndTime < threshold)
                current.Add(next);
            else
            {
                result.Add(current);
                current = new List<TripRecord> { next };
            }
        }
        result.Add(current);
        return result;
    }

    public DrivingScore GetBestScoreForGroup(List<TripRecord> group)
    {
        var scores = new List<DrivingScore>();
        // All single trips
        scores.AddRange(group.Select(AnalyzeScore));
        // All consecutive partitions (A+B, B+C, etc.)
        for (int i = 0; i < group.Count; i++)
        {
            for (int j = i + 1; j < group.Count; j++)
            {
                var concat = ConcatenateGroup(group.GetRange(i, j - i + 1));
                scores.Add(AnalyzeScore(concat));
            }
        }
        // Score of the whole group
        if (group.Count > 1)
            scores.Add(AnalyzeScore(ConcatenateGroup(group)));
        return scores.Min();
    }

    public TripRecord ConcatenateGroup(List<TripRecord> group)
    {
        return new TripRecord
        {
            Category = string.Join("/", group.Select(g => g.Category).Distinct()),
            StartTime = group.First().StartTime,
            StartOdometer = group.First().StartOdometer,
            StartAddress = group.First().StartAddress,
            EndTime = group.Last().EndTime,
            EndOdometer = group.Last().EndOdometer,
            EndAddress = group.Last().EndAddress,
            // FIX: Use sum of durations for scoring
            Duration = TimeSpan.FromTicks(group.Sum(g => g.Duration.Ticks)),
            DistanceKm = group.Sum(g => g.DistanceKm),
            FuelLiters = group.Sum(g => g.FuelLiters),
            UserNotes = string.Join("; ", group.Select(g => g.UserNotes).Where(n => !string.IsNullOrWhiteSpace(n)))
        };
    }
}

public class AppConfig
{
    public TripAnalysisParameters TripAnalysisParameters { get; set; } = new();
    public static AppConfig Load(string path)
    {
        if (!File.Exists(path)) return new AppConfig();
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.GetProperty("TripAnalysisParameters");
        return new AppConfig
        {
            TripAnalysisParameters = new TripAnalysisParameters
            {
                ColdStartThreshold = TimeSpan.FromMinutes(root.GetProperty("ColdStartThresholdMinutes").GetInt32()),
                ShortDuration = TimeSpan.FromMinutes(root.GetProperty("ShortDurationMinutes").GetInt32()),
                MediumDuration = TimeSpan.FromMinutes(root.GetProperty("MediumDurationMinutes").GetInt32()),
                LongDuration = TimeSpan.FromMinutes(root.GetProperty("LongDurationMinutes").GetInt32()),
                HeavyTrafficSpeed = root.GetProperty("HeavyTrafficSpeed").GetDouble(),
                ModerateCitySpeed = root.GetProperty("ModerateCitySpeed").GetDouble(),
                MixedSpeed = root.GetProperty("MixedSpeed").GetDouble(),
                DpfMovingAverageWeeks = root.TryGetProperty("DpfMovingAverageWeeks", out var dpfWeeksProp) ? dpfWeeksProp.GetInt32() : 3
            }
        };
    }
}

class Program
{
    const string JsonDbFile = "triprecords.json";
    const string ConfigFile = "appsettings.json";
    static ITripRepository repository = new JsonTripRepository(JsonDbFile);
    static TripImporter importer = new TripImporter();
    static TripSummaryService summaryService = new TripSummaryService();
    static AppConfig config = AppConfig.Load(ConfigFile);
    static TripAnalysisParameters analysisParams = config.TripAnalysisParameters;
    static TripAnalysisService analysisService = new TripAnalysisService(analysisParams);

    static void Main(string[] args)
    {
        repository.Load();
        while (true)
        {
            Console.WriteLine("\n--- Trip Record Manager ---");
            Console.WriteLine("1. Upload (import) new CSV file");
            Console.WriteLine("2. List all records (summary)");
            Console.WriteLine("3. Print summary");
            Console.WriteLine("4. Analyze trips");
            Console.WriteLine("5. Generate chart");
            Console.WriteLine("6. Visualize calendar heatmap");
            Console.WriteLine("7. Fuel consumption trend");
            Console.WriteLine("0. Exit");
            Console.Write("Enter command number: ");
            var input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    ImportCsvInteractive();
                    break;
                case "2":
                    ListRecordsSummary();
                    break;
                case "3":
                    summaryService.PrintSummary(repository.GetAll(), analysisService, analysisParams);
                    break;
                case "4":
                    analysisService.AnalyzeAndPrint(repository.GetAll());
                    break;
                case "5":
                    GenerateChart();
                    break;
                case "6":
                    VisualizeCalendarHeatmap();
                    break;
                case "7":
                    PlotFuelConsumptionTrend();
                    break;
                case "0":
                    repository.Save();
                    Console.WriteLine("Exiting.");
                    return;
                default:
                    Console.WriteLine("Unknown command.");
                    break;
            }
        }
    }

    static void ImportCsvInteractive()
    {
        Console.Write("Enter path to CSV file: ");
        var filePath = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine("File not found.");
            return;
        }
        var importedRecords = importer.ImportFromCsv(filePath).ToList();
        repository.AddRange(importedRecords, out var duplicates);
        repository.Save();
        Console.WriteLine($"Imported {importedRecords.Count - duplicates.Count} new records from '{filePath}'.");
        if (duplicates.Count > 0)
        {
            Console.WriteLine($"{duplicates.Count} duplicate records were not added:");
            foreach (var d in duplicates)
                Console.WriteLine($"  Duplicate: {d.Category} | {d.StartTime} - {d.EndTime}");
        }
    }

    static void ListRecordsSummary()
    {
        var records = repository.GetAll();
        Console.WriteLine($"Total records: {records.Count}");
        foreach (var r in records)
        {
            Console.WriteLine($"{r.Category} | {r.StartTime} | {r.DistanceKm} km | {r.FuelLiters} L");
        }
    }

    static void GenerateChart()
    {
        var trips = repository.GetAll();
        var groups = TripAnalysisService.ConcatenateTrips(trips.OrderBy(t => t.StartTime).ToList(), analysisParams.ColdStartThreshold);
        var analysisResults = groups.Select(g => new {
            Trip = analysisService.ConcatenateGroup(g),
            Score = analysisService.GetBestScoreForGroup(g)
        }).ToList();

        // Monthly aggregation by score (Bad=0, Fair=1, Good=2, Excellent=3)
        var months = analysisResults.Select(r => new DateTime(r.Trip.StartTime.Year, r.Trip.StartTime.Month, 1)).Distinct().OrderBy(d => d).ToList();
        var monthIndex = months.Select((d, i) => (d, i)).ToDictionary(x => x.d, x => x.i);
        int nMonths = months.Count;
        double[][] valuesByScore = new double[4][];
        for (int i = 0; i < 4; i++) valuesByScore[i] = new double[nMonths];
        foreach (var r in analysisResults)
        {
            var m = new DateTime(r.Trip.StartTime.Year, r.Trip.StartTime.Month, 1);
            int idx = monthIndex[m];
            int scoreIdx = 4 - (int)r.Score;
            valuesByScore[scoreIdx][idx] += r.Trip.DistanceKm;
        }
        string[] scoreNames = { "Bad", "Fair", "Good", "Excellent" };
        ScottPlot.Color[] scoreColors = {
            Colors.Red,
            Colors.Yellow,
            Colors.LightGreen,
            Colors.Green
        };

        ScottPlot.Plot myPlot = new();
        // Bars at integer X
        for (int x = 0; x < nMonths; x++)
        {
            double nextBarBase = 0;
            for (int i = 0; i < 4; i++)
            {
                double val = valuesByScore[i][x];
                if (val == 0) continue;
                ScottPlot.Bar bar = new()
                {
                    Value = nextBarBase + val,
                    FillColor = scoreColors[i],
                    ValueBase = nextBarBase,
                    Position = x,
                };
                myPlot.Add.Bar(bar);
                nextBarBase += val;
            }
        }
        // Custom tick labels for months
        ScottPlot.TickGenerators.NumericManual tickGen = new();
        for (int x = 0; x < nMonths; x++)
            tickGen.AddMajor(x, months[x].ToString("yyyy-MM"));
        myPlot.Axes.Bottom.TickGenerator = tickGen;
        // Legend
        for (int i = 0; i < 4; i++)
        {
            ScottPlot.LegendItem item = new()
            {
                LabelText = scoreNames[i],
                FillColor = scoreColors[i]
            };
            myPlot.Legend.ManualItems.Add(item);
        }
        myPlot.Legend.Orientation = ScottPlot.Orientation.Horizontal;
        myPlot.ShowLegend(ScottPlot.Alignment.UpperRight);
        myPlot.Axes.Margins(bottom: 0, top: .3);

        // DPF index line (right axis, 0-1 scale, X is fractional between months)
        var allWeeks = trips
            .Select(t => System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(t.StartTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday))
            .Zip(trips.Select(t => t.StartTime.Year), (w, y) => (Year: y, Week: w))
            .Distinct()
            .OrderBy(x => x.Year).ThenBy(x => x.Week)
            .ToList();

        var weekStartDates = allWeeks
            .Select(w => FirstDateOfWeekISO8601(w.Year, w.Week))
            .ToList();

        var dpfWeekly = new List<double>();

        for (int i = 0; i < allWeeks.Count; i++)
        {
            var weekStart = weekStartDates[i];
            var weekEnd = weekStart.AddDays(6);
            var weekGroups = groups.Where(g => g.Any(t => t.StartTime.Date >= weekStart && t.StartTime.Date <= weekEnd)).ToList();
            double goodKm = 0, allKm = 0, totalLiters = 0;
            foreach (var g in weekGroups)
            {
                var concatTrip = analysisService.ConcatenateGroup(g);
                var bestScore = analysisService.GetBestScoreForGroup(g);
                if (bestScore == DrivingScore.Excellent || bestScore == DrivingScore.Good)
                    goodKm += concatTrip.DistanceKm;
                allKm += concatTrip.DistanceKm;
                totalLiters += concatTrip.FuelLiters;
            }
            dpfWeekly.Add(allKm > 0 ? goodKm / allKm : 0);
        }

        // Moving average over N weeks
        int dpfWindow = analysisParams.DpfMovingAverageWeeks;
        var dpfMA = new List<double>();
        for (int i = 0; i < dpfWeekly.Count; i++)
        {
            int start = Math.Max(0, i - dpfWindow + 1);
            double avg = dpfWeekly.Skip(start).Take(i - start + 1).Average();
            dpfMA.Add(avg);
        }

        // Map each week to a fractional X between months
        double firstMonth = months[0].ToOADate();
        double lastMonth = months.Last().AddMonths(1).ToOADate();
        double monthSpan = (lastMonth - firstMonth) / nMonths;
        var dpfXs = weekStartDates.Select(d =>
        {
            int mIdx = months.FindLastIndex(m => d >= m);
            if (mIdx < 0) mIdx = 0;
            double monthStart = months[mIdx].ToOADate();
            double frac = (d.ToOADate() - monthStart) / monthSpan;
            return mIdx + frac;
        });
        var dpfLine = myPlot.Add.Scatter(dpfXs.ToArray(), dpfMA.ToArray(), color: Colors.Blue);
        dpfLine.Axes.YAxis = myPlot.Axes.Right;

        myPlot.Axes.Left.Label.Text = "Distance (km)";
        myPlot.Axes.Right.Label.Text = "DPF index";
        myPlot.Axes.Right.Label.ForeColor = Colors.Blue;
        myPlot.Axes.Right.Min = 0;
        myPlot.Axes.Right.Max = 1;
        myPlot.Axes.Left.Label.ForeColor = Colors.Black;
        myPlot.XLabel("Month");
        myPlot.Title("Monthly Distance by Score (stacked) and Weekly DPF Index");
        myPlot.SavePng("trip_stats.png", 1000, 600);
        Console.WriteLine("Chart saved as trip_stats.png");
    }

    static void VisualizeCalendarHeatmap()
    {
        Console.Write("Enter year (e.g. 2024): ");
        if (!int.TryParse(Console.ReadLine(), out int year))
        {
            Console.WriteLine("Invalid year.");
            return;
        }
        var trips = repository.GetAll();
        var groups = TripAnalysisService.ConcatenateTrips(trips.OrderBy(t => t.StartTime).ToList(), analysisParams.ColdStartThreshold);
        // Aggregate by day
        var dayDict = new Dictionary<DateTime, (DrivingScore score, double km)>();
        var minDate = new DateTime(year, 1, 1);
        var maxDate = new DateTime(year, 12, 31);
        for (var date = minDate; date <= maxDate; date = date.AddDays(1))
        {
            var dayGroups = groups.Where(g => g.Any(t => t.StartTime.Date == date)).ToList();
            if (dayGroups.Count == 0) continue;
            var bestScore = dayGroups.Min(g => analysisService.GetBestScoreForGroup(g));
            double totalKm = dayGroups.Sum(g => g.Where(t => t.StartTime.Date == date).Sum(t => t.DistanceKm));
            dayDict[date] = (bestScore, totalKm);
        }
        // Find week range
        var allMondays = Enumerable.Range(0, 53)
            .Select(i => FirstDateOfWeekISO8601(year, i + 1))
            .Where(d => d.Year == year || (d.Year == year - 1 && d.Month == 12))
            .ToList();
        int weekCount = allMondays.Count;
        // Prepare data arrays
        double[,] data = new double[7, weekCount]; // [day, week]
        double[,] kmData = new double[7, weekCount];
        double maxKm = dayDict.Count > 0 ? dayDict.Max(x => x.Value.km) : 1.0;
        foreach (var kvp in dayDict)
        {
            var date = kvp.Key;
            int weekIdx = allMondays.FindIndex(m => m <= date && date < m.AddDays(7));
            if (weekIdx < 0) continue;
            int dow = ((int)date.DayOfWeek + 6) % 7; // Monday=0, Sunday=6
            double scale = Math.Min(1, kvp.Value.km / 100);
            data[dow, weekIdx] = scale * (1 + (4 - (int)kvp.Value.score));
            kmData[dow, weekIdx] = kvp.Value.km;
        }

        // 2. Build color array
        ScottPlot.Color[] baseColors = {
            Colors.Transparent, // 0: no usage
            Colors.Red,       // 1: bad
            Colors.Yellow,    // 2: fair
            Colors.LightGreen,// 3: good
            Colors.Green      // 4: excellent
        };
        int n = 25;
        ScottPlot.Color[] colorMap = new ScottPlot.Color[1+4*n];
        colorMap[0] = baseColors[0];
        for (int i = 1; i <= n; i++)
        {
            for (int b = 1; b <= 4; b++)
            {
                int argb = (byte)(255.0/n*i) << 24 | baseColors[b].R << 16 | baseColors[b].G << 8 | baseColors[b].B;
                colorMap[i * b] = Color.FromARGB(argb);
            } 
        }

        // Plot
        ScottPlot.Plot myPlot = new();
        var hm = myPlot.Add.Heatmap(data);
        hm.Colormap = new ColorMap(colorMap);
        // Set rectangle: x=weeks as dates, y=0..7
        CoordinateRange yRange = new(0, 7);
        CoordinateRange xRange = new(allMondays.First().ToOADate(), allMondays.Last().AddDays(7).ToOADate());
        hm.Rectangle = new(xRange, yRange);
        myPlot.Axes.DateTimeTicksBottom();
        myPlot.XLabel("Week");
        myPlot.YLabel("Day of week");
        myPlot.Title($"Car Usage Calendar Heatmap: {year}");

        var yTickGen = new ScottPlot.TickGenerators.NumericManual();
        var yPositions = Enumerable.Range(0, 7).Select(i => (double)i + 0.5).ToArray();
        var yLabels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        for (int i = 0; i < yPositions.Length; i++)
            yTickGen.AddMajor(yPositions[i], yLabels[6-i]);
        myPlot.Axes.Left.TickGenerator = yTickGen;

        var filename = $"calendar_heatmap_{year}.png";
        myPlot.SavePng(filename, 1400, 350);
        Console.WriteLine($"Calendar heatmap saved as {filename}");
    }

    static void PlotFuelConsumptionTrend()
    {
        var trips = repository.GetAll();

        var groups = TripAnalysisService.ConcatenateTrips(
            trips.OrderBy(t => t.StartTime).ToList(),
            analysisParams.ColdStartThreshold);

        var data = groups.Select(g =>
        {
            var t = analysisService.ConcatenateGroup(g);
            double fuelPer100 = t.DistanceKm > 0 ? (t.FuelLiters / t.DistanceKm) * 100.0 : 0;

            return new
            {
                Time = t.StartTime,
                FuelPer100 = fuelPer100,
                Km = t.DistanceKm
            };
        })
        .Where(x => x.Km > 0)
        .OrderBy(x => x.Time)
        .ToList();

        var filtered = data
            .Where(d =>
                d.Km > 0 &&
                d.FuelPer100 > 1 &&
                d.FuelPer100 <= 30)
            .OrderBy(d => d.Time)
            .ToList();

        if (filtered.Count == 0)
        {
            Console.WriteLine("No valid data after filtering.");
            return;
        }

        double[] xs = filtered.Select(d => d.Time.ToOADate()).ToArray();
        double[] ys = filtered.Select(d => d.FuelPer100).ToArray();
        double[] weights = filtered.Select(d => d.Km).ToArray();

        ScottPlot.Plot plot = new();

        // scatter points sized by distance
        for (int i = 0; i < xs.Length; i++)
        {
            double size = 0.5 + 1.2 * Math.Sqrt(weights[i]);
            var marker = plot.Add.Marker(xs[i], ys[i]);
            marker.Size = (float)size;
            marker.Color = Colors.DarkBlue.WithAlpha(.6);
        }

        // LOWESS smoothing
        double[] smoothY = WeightedLowess(xs, ys, weights, 0.25);

        var smoothLine = plot.Add.Scatter(xs, smoothY);
        smoothLine.Color = Colors.Red;
        smoothLine.LineWidth = 3;

        plot.Axes.DateTimeTicksBottom();
        plot.XLabel("Time");
        plot.YLabel("Fuel consumption (L / 100 km)");
        plot.Title("Fuel Consumption Trend (LOWESS)");

        plot.SavePng("fuel_trend.png", 1000, 600);

        Console.WriteLine("Fuel trend chart saved as fuel_trend.png");
    }

    static double[] WeightedLowess(double[] x, double[] y, double[] weights, double frac)
    {
        int n = x.Length;
        int span = Math.Max(2, (int)(frac * n));

        double[] ySmooth = new double[n];

        for (int i = 0; i < n; i++)
        {
            double xi = x[i];

            // distances to other points
            var distances = x.Select(v => Math.Abs(v - xi)).ToArray();
            double maxDist = distances.OrderBy(d => d).ElementAt(span);

            double sumW = 0;
            double sumWX = 0;
            double sumWY = 0;
            double sumWXX = 0;
            double sumWXY = 0;

            for (int j = 0; j < n; j++)
            {
                double dist = distances[j] / maxDist;

                if (dist >= 1)
                    continue;

                // tricube kernel
                double kernel = Math.Pow(1 - Math.Pow(dist, 3), 3);

                double w = kernel * weights[j];

                sumW += w;
                sumWX += w * x[j];
                sumWY += w * y[j];
                sumWXX += w * x[j] * x[j];
                sumWXY += w * x[j] * y[j];
            }

            double slope =
                (sumW * sumWXY - sumWX * sumWY) /
                (sumW * sumWXX - sumWX * sumWX);

            double intercept =
                (sumWY - slope * sumWX) / sumW;

            ySmooth[i] = slope * xi + intercept;
        }

        return ySmooth;
    }

    class ColorMap : ScottPlot.IColormap
    {
        private readonly ScottPlot.Color[] _colors;
        public ColorMap(ScottPlot.Color[] colors) => _colors = colors;

        public string Name => "CarUsage";

        public ScottPlot.Color GetColor(double value)
        {
            var x = value * _colors.Length; // scale to number of colors
            var idx = (int)x;
            if (idx < 0) idx = 0;
            if (idx >= _colors.Length) idx = _colors.Length - 1;
            return _colors[idx]; // Y is ignored since we have precomputed colors
        }
    }

    static DateTime FirstDateOfWeekISO8601(int year, int weekOfYear)
    {
        DateTime jan4 = new DateTime(year, 1, 4);
        int daysOffset = DayOfWeek.Monday - jan4.DayOfWeek;
        DateTime firstMonday = jan4.AddDays(daysOffset);
        return firstMonday.AddDays((weekOfYear - 1) * 7);
    }
}
