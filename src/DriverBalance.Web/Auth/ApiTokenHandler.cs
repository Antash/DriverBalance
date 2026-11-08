using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using DriverBalance.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DriverBalance.Web;

/// <summary>Token format and hashing: "dbk_" + base64url(32 random bytes); only the SHA-256 (hex) is stored.</summary>
public static class ApiTokens
{
    public const string Prefix = "dbk_";

    public static string Generate() => Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Authenticates `Authorization: Bearer dbk_...` for /mcp only; it is never the default scheme.</summary>
public sealed class ApiTokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    DriverBalanceDbContext db, DbContextOptions<DriverBalanceDbContext> dbOptions, TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (!header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var token = header[bearer.Length..].Trim();
        if (!token.StartsWith(ApiTokens.Prefix, StringComparison.Ordinal)) return AuthenticateResult.Fail("Not an API token.");

        var hash = ApiTokens.Hash(token);
        var found = await FindByHashAsync(hash);
        if (found is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(found.TokenHash), Encoding.ASCII.GetBytes(hash)))
            return AuthenticateResult.Fail("Unknown or revoked token.");

        await TouchAsync(found.Id, found.UserId);

        var identity = new ClaimsIdentity([new Claim("sub", found.UserId.ToString())], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }

    private sealed record TokenMatch(long Id, long UserId, string TokenHash);

    /// <summary>
    /// The single sanctioned bypass of the per-user query filter: the token has to be found before its owner is known.
    /// Matching is by the unique SHA-256 hash only, and nothing but the id, owner and hash leaves this method.
    /// </summary>
    private Task<TokenMatch?> FindByHashAsync(string hash) =>
        db.ApiTokens.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.TokenHash == hash)
            .Select(t => new TokenMatch(t.Id, t.UserId, t.TokenHash))
            .FirstOrDefaultAsync(Context.RequestAborted);

    /// <summary>Uses a context scoped to the token's own user, so the write guard stays fully in force.</summary>
    private async Task TouchAsync(long tokenId, long userId)
    {
        await using var userDb = new DriverBalanceDbContext(dbOptions, new TokenUser(userId));
        var row = await userDb.ApiTokens.FirstOrDefaultAsync(t => t.Id == tokenId, Context.RequestAborted);
        if (row is null) return;
        row.LastUsedAt = clock.GetUtcNow().UtcDateTime;
        await userDb.SaveChangesAsync(Context.RequestAborted);
    }

    private sealed record TokenUser(long UserId) : ICurrentUser;
}
