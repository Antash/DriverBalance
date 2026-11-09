using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace DriverBalance.VolvoSync;

internal sealed class SyncException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The whole browser flow against volvocars.com: open, sign in, set range, export, download.</summary>
internal sealed class VolvoJournal : IAsyncDisposable
{
    private const string AccountUrl = "https://www.volvocars.com/cz/account/my-cars/";
    private const string AuthHost = "auth.volvocars.com";
    private const string Dialog = "[role=dialog], dialog[open]";
    private const string Submit = "button[type=submit], input[type=submit]";

    private readonly Options _options;
    private readonly CredentialStore _credentials;
    private readonly bool _headed;
    private IPlaywright? _pw;
    private IBrowserContext? _context;
    private IPage? _page;
    private string? _journalUrl;

    public VolvoJournal(Options options, CredentialStore credentials, bool headed)
    {
        _options = options;
        _credentials = credentials;
        _headed = headed;
    }

    private IPage Page => _page ?? throw new InvalidOperationException("Browser not started.");
    private bool OnLoginPage => Page.Url.Contains(AuthHost, StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs one named step; a timeout becomes an error that names the step.</summary>
    private static async Task<T> RunStep<T>(string name, Func<Task<T>> body)
    {
        try { return await body(); }
        catch (TimeoutException e) { throw new SyncException($"Timed out during step '{name}'.", e); }
        catch (PlaywrightException e) when (e.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        { throw new SyncException($"Timed out during step '{name}'.", e); }
    }

    private static Task RunStep(string name, Func<Task> body) => RunStep<bool>(name, async () => { await body(); return true; });

    // ---- step: start the persistent Edge profile ----
    public Task StartAsync() => RunStep("start Edge", async () =>
    {
        Directory.CreateDirectory(AppPaths.BrowserProfile);
        _pw = await Playwright.CreateAsync();
        _context = await _pw.Chromium.LaunchPersistentContextAsync(AppPaths.BrowserProfile, new BrowserTypeLaunchPersistentContextOptions
        {
            Channel = "msedge",
            Headless = !_headed,
            AcceptDownloads = true,
            Locale = "cs-CZ",
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
            Timeout = 60_000,
        });
        _context.SetDefaultTimeout(30_000);
        _page = _context.Pages.FirstOrDefault() ?? await _context.NewPageAsync();
    });

    // ---- step: open the journal (the car id is discovered, it may change) ----
    public Task OpenJournalAsync() => RunStep("open journal", async () =>
    {
        _journalUrl ??= await DiscoverJournalUrlAsync();
        await Page.GotoAsync(_journalUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await SettleAsync();
    });

    private async Task<string> DiscoverJournalUrlAsync()
    {
        await Page.GotoAsync(AccountUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await SettleAsync();
        if (OnLoginPage)
        {
            // Not signed in: the car id cannot be discovered until after sign-in.
            return _options.CarId is { } id ? JournalUrl(id) : AccountUrl;
        }

        await DismissCookieBannerAsync();
        var link = Page.Locator("a[href$='/journal-log/'], a[href$='/journal-log']").First;
        try
        {
            await link.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 15_000 });
            var href = await link.GetAttributeAsync("href");
            if (!string.IsNullOrEmpty(href)) return new Uri(new Uri(AccountUrl), href).ToString();
        }
        catch (TimeoutException) { }

        if (_options.CarId is { } fallback) return JournalUrl(fallback);
        throw new SyncException("Could not find the journal link on the account page and no --car-id was given.");
    }

    private static string JournalUrl(string carId) => $"{AccountUrl}{carId}/journal-log/";

    // ---- step: make sure we are signed in and on the journal ----
    public async Task EnsureSignedInAsync(bool interactive)
    {
        if (OnLoginPage)
        {
            if (interactive)
            {
                Console.WriteLine("Sign in to Volvo in the browser window (tick 'Pamatovat si me'). Waiting up to 10 minutes...");
                await RunStep("wait for manual sign-in", () => Page.WaitForURLAsync(
                    u => !u.Contains(AuthHost, StringComparison.OrdinalIgnoreCase), new PageWaitForURLOptions { Timeout = 600_000 }));
                await SettleAsync();
            }
            else
            {
                var creds = _credentials.Load()
                    ?? throw new SyncException("Volvo session expired and no credentials are stored. Run `login` (or `set-credentials`) first.");
                await SignInAsync(creds);
            }
        }
        await EnsureOnJournalAsync();
    }

    private Task EnsureOnJournalAsync() => RunStep("show journal page", async () =>
    {
        if (!Page.Url.Contains("/journal-log", StringComparison.OrdinalIgnoreCase))
        {
            // Signed in but landed elsewhere (e.g. the account page): rediscover the journal link.
            _journalUrl = null;
            await OpenJournalAsync();
            if (OnLoginPage) throw new SyncException("Still on the Volvo sign-in page after signing in; run `login`.");
        }
        await Page.Locator("input[name=filter-start-date]").WaitForAsync(new LocatorWaitForOptions { Timeout = 45_000 });
    });

    private Task SignInAsync(Credentials creds) => RunStep("sign in", async () =>
    {
        await DismissCookieBannerAsync();
        await Page.Locator("input[type=email], input[name=username], input[name=email]").First.FillAsync(creds.Email);
        var password = Page.Locator("input[type=password]").First;
        if (!await password.IsVisibleAsync())
        {
            // Some Keycloak themes ask for the e-mail first, then the password.
            await Page.Locator(Submit).First.ClickAsync();
            await password.WaitForAsync();
        }
        await password.FillAsync(creds.Password);
        var remember = Page.Locator("input[type=checkbox]").First;
        if (await remember.CountAsync() > 0 && !await remember.IsCheckedAsync())
            await remember.SetCheckedAsync(true, new LocatorSetCheckedOptions { Force = true });
        await Page.Locator(Submit).First.ClickAsync();

        // Either we leave the auth host, or a one-time code is requested.
        var code = Page.Locator("input[autocomplete=one-time-code], input[name*=otp i], input[name*=code i], input[id*=otp i]").First;
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (!OnLoginPage) { await SettleAsync(); return; }
            if (await code.IsVisibleAsync()) { await EnterCodeAsync(code); return; }
            if (await Page.Locator("[role=alert]").First.IsVisibleAsync())
                throw new SyncException("Volvo rejected the sign-in (wrong credentials?). Run `login` to sign in by hand.");
            await Page.WaitForTimeoutAsync(500);
        }
        throw new TimeoutException();
    });

    private async Task EnterCodeAsync(ILocator code)
    {
        if (Console.IsInputRedirected)
            throw new SyncException("Volvo asked for an e-mail code, but there is no console to type it into. Run `login`.");
        Console.Write("Volvo sent a one-time code by e-mail. Enter it: ");
        var value = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(value)) throw new SyncException("No code entered.");
        await code.FillAsync(value);
        await Page.Locator(Submit).First.ClickAsync();
        await Page.WaitForURLAsync(u => !u.Contains(AuthHost, StringComparison.OrdinalIgnoreCase), new PageWaitForURLOptions { Timeout = 60_000 });
        await SettleAsync();
    }

    // ---- step: set the date range ----
    public Task SetRangeAsync(DateRange range) => RunStep("set date range", async () =>
    {
        await DismissCookieBannerAsync();
        await SetDateAsync("filter-start-date", range.From);
        await SetDateAsync("filter-end-date", range.To);
        await RunStep("wait for list reload", async () =>
        {
            await Page.WaitForURLAsync(u => u.Contains($"startDate={range.From:yyyy-MM-dd}") && u.Contains($"endDate={range.To:yyyy-MM-dd}"),
                new PageWaitForURLOptions { Timeout = 30_000 });
            await SettleAsync();
        });
    });

    private async Task SetDateAsync(string name, DateOnly date)
    {
        var value = date.ToString("yyyy-MM-dd");
        if (await Page.Locator($"input[name={name}]").InputValueAsync() == value) return;
        await Page.EvaluateAsync("""
            ([name, value]) => {
              const el = document.querySelector(`input[name=${name}]`);
              const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
              setter.call(el, value);
              el.dispatchEvent(new Event('input', { bubbles: true }));
              el.dispatchEvent(new Event('change', { bubbles: true }));
            }
            """, new object[] { name, value });
        await SettleAsync();
    }

    // ---- steps: open the export dialog, then download ----
    public async Task ExportAsync(DateRange range, string destination)
    {
        await RunStep("open export dialog", async () =>
        {
            await Page.GetByRole(AriaRole.Button, new() { Name = "Exportovat jízdy" }).First.ClickAsync();
            await Page.Locator(Dialog).First.WaitForAsync();
        });

        await RunStep("check export interval", () => CheckIntervalAsync(range));

        await RunStep("download export", async () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
            var button = Page.Locator(Dialog).GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Exportovat vybran", RegexOptions.IgnoreCase) }).First;
            var download = await Page.RunAndWaitForDownloadAsync(() => button.ClickAsync(), new PageRunAndWaitForDownloadOptions { Timeout = 60_000 });
            await download.SaveAsAsync(destination);
        });
    }

    private async Task CheckIntervalAsync(DateRange range)
    {
        var url = Page.Url;
        if (!url.Contains($"startDate={range.From:yyyy-MM-dd}") || !url.Contains($"endDate={range.To:yyyy-MM-dd}"))
            throw new SyncException("The journal page interval does not match the requested range; refusing to export.");

        // Best effort on the dialog itself: its text or inputs should mention the start date in some format.
        var dialog = Page.Locator(Dialog).First;
        var values = await dialog.Locator("input").EvaluateAllAsync<string[]>("els => els.map(e => e.value)");
        var hay = await dialog.InnerTextAsync() + " " + string.Join(' ', values);
        var d = range.From;
        string[] forms = [$"{d:yyyy-MM-dd}", $"{d:dd.MM.yyyy}", $"{d.Day}. {d.Month}. {d.Year}", $"{d:dd. MM. yy}", $"{d:dd. MM. yyyy}", $"{d.Day}.{d.Month}.{d.Year}"];
        if (!forms.Any(hay.Contains))
            Console.WriteLine("Warning: could not confirm the interval inside the export dialog; the page interval matched.");
    }

    // ---- helpers ----
    private async Task SettleAsync()
    {
        try { await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15_000 }); }
        catch (TimeoutException) { /* analytics keep the network busy; the explicit waits that follow are what matter */ }
    }

    private async Task DismissCookieBannerAsync()
    {
        try
        {
            var accept = Page.Locator("#onetrust-accept-btn-handler");
            if (await accept.IsVisibleAsync()) await accept.ClickAsync(new LocatorClickOptions { Timeout = 3_000 });
        }
        catch (PlaywrightException) { }
    }

    /// <summary>Saves a full-page screenshot to the exports folder; returns its path, or null if impossible.</summary>
    public async Task<string?> SaveFailureScreenshotAsync()
    {
        if (_page is null) return null;
        try
        {
            Directory.CreateDirectory(AppPaths.ExportsDir);
            var path = Path.Combine(AppPaths.ExportsDir, ExportNaming.FailureScreenshotName(DateTime.Now));
            await _page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
            return path;
        }
        catch { return null; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null) { try { await _context.CloseAsync(); } catch { } }
        _pw?.Dispose();
    }
}
