using System.Net.Http.Headers;
using System.Text.Json;

namespace DriverBalance.VolvoSync;

internal sealed record ImportOutcome(int Imported, int Skipped, DateOnly? FirstTrip, DateOnly? LastTrip, string[] Warnings);

/// <summary>
/// Talks to the DriverBalance API. The CrossSiteGuard needs an Origin header. A local app needs no token (loopback is the
/// local user); a hosted app needs an API token, sent as a bearer token when given (read from <see cref="TokenVariable"/>,
/// never a command-line argument, so it does not show up in process listings).
/// </summary>
internal sealed class AppClient : IDisposable
{
    /// <summary>Environment variable holding the DriverBalance API token (Settings, API tokens).</summary>
    public const string TokenVariable = "DRIVERBALANCE_TOKEN";

    private readonly string appUrl;
    private readonly long vehicleId;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AppClient(string appUrl, long vehicleId, string? token = null, HttpMessageHandler? handler = null)
    {
        this.appUrl = appUrl;
        this.vehicleId = vehicleId;
        // 90 s: a hosted app on a free plan may be asleep and take a while to wake up.
        _http = new HttpClient(handler ?? new HttpClientHandler()) { BaseAddress = new Uri(appUrl + "/"), Timeout = TimeSpan.FromSeconds(90) };
        if (!string.IsNullOrWhiteSpace(token)) _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
    }

    public async Task<DateOnly?> GetLastTripAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"api/vehicles/{vehicleId}/import/last-trip");
        req.Headers.Add("Origin", appUrl);
        using var res = await _http.SendAsync(req, ct);
        await EnsureOkAsync(res, "reading the last trip date", ct);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var p = doc.RootElement.GetProperty("lastTrip");
        return p.ValueKind == JsonValueKind.String ? DateOnly.Parse(p.GetString()!) : null;
    }

    public async Task<ImportOutcome> ImportCsvAsync(string csvPath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(csvPath);
        using var content = new MultipartFormDataContent();
        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", Path.GetFileName(csvPath));
        using var req = new HttpRequestMessage(HttpMethod.Post, $"api/vehicles/{vehicleId}/import/csv") { Content = content };
        req.Headers.Add("Origin", appUrl);
        using var res = await _http.SendAsync(req, ct);
        await EnsureOkAsync(res, "importing the CSV", ct);
        var r = JsonSerializer.Deserialize<Result>(await res.Content.ReadAsStringAsync(ct), Json)!;
        return new ImportOutcome(r.Imported, r.Skipped, r.FirstTrip, r.LastTrip, r.Warnings ?? []);
    }

    private sealed record Result(int Imported, int Skipped, DateOnly? FirstTrip, DateOnly? LastTrip, string[]? Warnings);

    private static async Task EnsureOkAsync(HttpResponseMessage res, string what, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var body = await res.Content.ReadAsStringAsync(ct);
        throw new SyncException($"The app returned {(int)res.StatusCode} while {what}: {Truncate(body)}");
    }

    private static string Truncate(string s) => s.Length <= 300 ? s : s[..300] + "...";

    public void Dispose() => _http.Dispose();
}
