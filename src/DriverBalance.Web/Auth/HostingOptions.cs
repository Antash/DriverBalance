namespace DriverBalance.Web;

public enum AppMode { Local, Hosted }

/// <summary>
/// How the app is run. Local (default): the laptop setup, every loopback request is user 1, no Google. Hosted: Google
/// sign-in with cookies plus API tokens only; the LocalUser scheme is not registered at all.
/// Settings: DriverBalance:Mode, DriverBalance:AdminEmail, DriverBalance:Google:ClientId, DriverBalance:Google:ClientSecret
/// (in Azure: DriverBalance__Mode, ...). Secrets live in app settings only, never in the repository.
/// </summary>
public sealed class HostingOptions
{
    public AppMode Mode { get; init; } = AppMode.Local;
    public string? AdminEmail { get; init; }
    public string? GoogleClientId { get; init; }
    public string? GoogleClientSecret { get; init; }

    public bool IsHosted => Mode == AppMode.Hosted;

    public static HostingOptions From(IConfiguration c)
    {
        var mode = c["DriverBalance:Mode"];
        if (!string.IsNullOrWhiteSpace(mode) && !Enum.TryParse<AppMode>(mode.Trim(), ignoreCase: true, out _))
            throw new InvalidOperationException($"DriverBalance:Mode must be Local or Hosted, not '{mode}'.");
        return new HostingOptions
        {
            Mode = string.IsNullOrWhiteSpace(mode) ? AppMode.Local : Enum.Parse<AppMode>(mode.Trim(), ignoreCase: true),
            AdminEmail = Normalise(c["DriverBalance:AdminEmail"]),
            GoogleClientId = Blank(c["DriverBalance:Google:ClientId"]),
            GoogleClientSecret = Blank(c["DriverBalance:Google:ClientSecret"]),
        };
    }

    /// <summary>Hosted mode refuses to start without what it needs to sign people in; the message names the missing settings.</summary>
    public void Validate()
    {
        if (!IsHosted) return;
        var missing = new List<string>();
        if (GoogleClientId is null) missing.Add("DriverBalance:Google:ClientId");
        if (GoogleClientSecret is null) missing.Add("DriverBalance:Google:ClientSecret");
        if (AdminEmail is null) missing.Add("DriverBalance:AdminEmail");
        if (missing.Count > 0)
            throw new InvalidOperationException($"Hosted mode needs these settings: {string.Join(", ", missing)}.");
    }

    public bool IsAdminEmail(string? email) => AdminEmail is not null && Normalise(email) == AdminEmail;

    /// <summary>Lower-case, trimmed; null for blank. Emails are compared in this form everywhere.</summary>
    public static string? Normalise(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
