using DriverBalance.Data;

namespace DriverBalance.Web;

/// <summary>Files for expense documents on the local disk: <c>&lt;root&gt;/&lt;vehicleId&gt;/&lt;expenseId&gt;/&lt;docId&gt;-&lt;name&gt;</c>.</summary>
public sealed class DocumentStore(IConfiguration config, ILogger<DocumentStore> log)
{
    public const string ConfigKey = "DriverBalance:DocumentsPath";
    public const long MaxBytes = 20L * 1024 * 1024;
    public const int MaxFiles = 10;

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
        [".png"] = "image/png", [".webp"] = "image/webp", [".heic"] = "image/heic",
    };

    private static readonly HashSet<string> AllowedTypes = new(ByExtension.Values, StringComparer.OrdinalIgnoreCase);

    private readonly Lazy<string> _root = new(() =>
    {
        var configured = config[ConfigKey];
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
        var db = Path.GetFullPath(DbSetup.ResolvePath(config));
        return Path.Combine(Path.GetDirectoryName(db)!, "documents");
    });

    public string Root => _root.Value;

    /// <summary>The normalised content type, or null when the file is not an accepted document type.</summary>
    public static string? NormaliseType(string? contentType, string fileName)
    {
        var type = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        if (type == "image/jpg") type = "image/jpeg";
        if (AllowedTypes.Contains(type)) return type;
        if (type is "" or "application/octet-stream" && ByExtension.TryGetValue(Path.GetExtension(fileName), out var byExt)) return byExt;
        return null;
    }

    private static string LastPart(string? name) => (name ?? "").Replace('\\', '/').Split('/').Last();

    /// <summary>The name shown to the user: path parts and control characters removed, at most 255 characters.</summary>
    public static string DisplayName(string? name)
    {
        var n = new string(LastPart(name).Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (n.Length > 255) n = n[..255];
        return n.Length == 0 ? "document" : n;
    }

    /// <summary>The name used on disk: only letters, digits and . - _ space, at most 100 characters.</summary>
    public static string SafeName(string? name)
    {
        var n = new string(LastPart(name).Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ').ToArray()).Trim();
        if (n.Length > 100) n = n[..100];
        return n.Length == 0 ? "document" : n;
    }

    public string ExpenseDir(long vehicleId, long expenseId) => Path.Combine(Root, vehicleId.ToString(), expenseId.ToString());

    public string PathOf(ExpenseDocumentRow d) => Path.Combine(ExpenseDir(d.VehicleId, d.ExpenseId), $"{d.Id}-{d.StoredName}");

    public async Task SaveAsync(ExpenseDocumentRow d, IFormFile file, CancellationToken ct)
    {
        Directory.CreateDirectory(ExpenseDir(d.VehicleId, d.ExpenseId));
        await using var target = new FileStream(PathOf(d), FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(target, ct);
    }

    public FileStream? Open(ExpenseDocumentRow d)
    {
        var path = PathOf(d);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    /// <summary>Removes one file (and its folder when empty); IO errors are logged, not thrown.</summary>
    public void DeleteFile(ExpenseDocumentRow d)
    {
        try
        {
            File.Delete(PathOf(d));
            var dir = ExpenseDir(d.VehicleId, d.ExpenseId);
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(e, "Could not delete document file {Path}", PathOf(d));
        }
    }

    /// <summary>Removes every document of one vehicle (used when a whole user is deleted); IO errors are logged, not thrown.</summary>
    public void DeleteVehicleFolder(long vehicleId)
    {
        try
        {
            var dir = Path.Combine(Root, vehicleId.ToString());
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(e, "Could not delete document folder of vehicle {VehicleId}", vehicleId);
        }
    }

    public void DeleteExpenseFolder(long vehicleId, long expenseId)
    {
        try
        {
            var dir = ExpenseDir(vehicleId, expenseId);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(e, "Could not delete document folder of expense {ExpenseId}", expenseId);
        }
    }
}
