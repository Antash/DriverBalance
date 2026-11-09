using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DriverBalance.VolvoSync;

internal sealed record Credentials(string Email, string Password)
{
    public override string ToString() => $"Credentials {{ Email = {Email}, Password = ***** }}";
}

/// <summary>Credentials encrypted with Windows DPAPI for the current user.</summary>
internal sealed class CredentialStore(string path)
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DriverBalance.VolvoSync");

    public static string DefaultPath => Path.Combine(AppPaths.DataDir, "volvo-credentials.bin");

    public void Save(Credentials c)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var plain = JsonSerializer.SerializeToUtf8Bytes(new[] { c.Email, c.Password });
        File.WriteAllBytes(path, ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
    }

    public Credentials? Load()
    {
        if (!File.Exists(path)) return null;
        var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        var parts = JsonSerializer.Deserialize<string[]>(plain);
        return parts is { Length: 2 } ? new Credentials(parts[0], parts[1]) : null;
    }

    public bool Delete()
    {
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }
}

internal static class AppPaths
{
    public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriverBalance");
    public static string BrowserProfile => Path.Combine(DataDir, "volvo-browser");
    public static string ExportsDir => Path.Combine(DataDir, "volvo-exports");
}
