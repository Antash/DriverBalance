namespace DriverBalance.VolvoSync;

internal static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        Options options;
        try { options = Options.Parse(args); }
        catch (UsageException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine("Use --help for usage.");
            return 2;
        }
        if (options.Help) { Console.WriteLine(Options.HelpText); return 0; }

        var store = new CredentialStore(CredentialStore.DefaultPath);
        try
        {
            switch (options.Command)
            {
                case "set-credentials": return SetCredentials(store);
                case "clear-credentials":
                    Console.WriteLine(store.Delete() ? "Stored credentials deleted." : "No stored credentials.");
                    return 0;
                case "login": return await LoginAsync(options, store);
                default: return await SyncAsync(options, store);
            }
        }
        catch (SyncException e)
        {
            Console.Error.WriteLine("Error: " + e.Message);
            return 1;
        }
        catch (HttpRequestException e)
        {
            Console.Error.WriteLine($"Error: cannot reach the DriverBalance app at {options.AppUrl} ({e.Message}). Is it running?");
            return 1;
        }
        catch (Exception e) when (e is Microsoft.Playwright.PlaywrightException or IOException or System.Security.Cryptography.CryptographicException)
        {
            Console.Error.WriteLine($"Error: {e.GetType().Name}: {e.Message.Split('\n')[0]}");
            return 1;
        }
    }

    private static int SetCredentials(CredentialStore store)
    {
        Console.Write("Volvo e-mail: ");
        var email = Console.ReadLine()?.Trim();
        Console.Write("Volvo password (not shown): ");
        var password = ReadPassword();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("E-mail and password are both required; nothing stored.");
            return 1;
        }
        store.Save(new Credentials(email, password));
        Console.WriteLine($"Credentials stored (encrypted for this Windows user) in {CredentialStore.DefaultPath}.");
        return 0;
    }

    private static string ReadPassword()
    {
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return sb.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; }
            else if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
        }
    }

    private static async Task<int> LoginAsync(Options options, CredentialStore store)
    {
        await using var journal = new VolvoJournal(options, store, headed: true);
        try
        {
            await journal.StartAsync();
            await journal.OpenJournalAsync();
            await journal.EnsureSignedInAsync(interactive: true);
            Console.WriteLine("Signed in; the Volvo session is stored in the browser profile.");
            return 0;
        }
        catch (Exception)
        {
            var shot = await journal.SaveFailureScreenshotAsync();
            if (shot is not null) Console.Error.WriteLine($"Screenshot saved: {shot}");
            throw;
        }
    }

    private static async Task<int> SyncAsync(Options options, CredentialStore store)
    {
        using var app = new AppClient(options.AppUrl, options.VehicleId);
        var lastTrip = options.From is null ? await app.GetLastTripAsync(CancellationToken.None) : null;
        var range = SyncRange.Compute(options.From, lastTrip, DateOnly.FromDateTime(DateTime.Now));
        Console.WriteLine($"Exporting Volvo trips {range.From:yyyy-MM-dd} to {range.To:yyyy-MM-dd}.");

        var file = Path.Combine(AppPaths.ExportsDir, ExportNaming.ExportFileName(range.From, range.To));
        await using var journal = new VolvoJournal(options, store, options.Headed);
        try
        {
            await journal.StartAsync();
            await journal.OpenJournalAsync();
            await journal.EnsureSignedInAsync(interactive: false);
            await journal.SetRangeAsync(range);
            await journal.ExportAsync(range, file);
            Console.WriteLine($"Saved {file}");

            var r = await app.ImportCsvAsync(file, CancellationToken.None);
            Console.WriteLine($"Imported {r.Imported}, skipped {r.Skipped} duplicates. Trips {r.FirstTrip:yyyy-MM-dd} to {r.LastTrip:yyyy-MM-dd}.");
            foreach (var w in r.Warnings) Console.WriteLine("Warning: " + w);
            if (options.KeepOpen) { Console.WriteLine("--keep-open: press Enter to close the browser."); Console.ReadLine(); }
            return 0;
        }
        catch (Exception)
        {
            var shot = await journal.SaveFailureScreenshotAsync();
            if (shot is not null) Console.Error.WriteLine($"Screenshot saved: {shot}");
            if (options.KeepOpen) { Console.Error.WriteLine("--keep-open: press Enter to close the browser."); Console.ReadLine(); }
            throw;
        }
    }
}
