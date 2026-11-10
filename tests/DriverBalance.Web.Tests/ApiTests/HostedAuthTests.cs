using System.Net;
using System.Net.Http.Json;
using DriverBalance.Core;
using DriverBalance.Data;
using DriverBalance.Web.Tests.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DriverBalance.Web.Tests.ApiTests;

/// <summary>A clock the test can move forward.</summary>
public sealed class AdjustableClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>The sign-in rules, without Google: who gets a profile, who is refused, who is the admin.</summary>
public sealed class UserProvisionerTests : IDisposable
{
    private const string Admin = "owner@example.com";
    private readonly TestDb _db = new();

    private UserProvisioner Provisioner(DateTimeOffset? now = null) => new(
        new DbContextOptionsBuilder<DriverBalanceDbContext>().UseSqlite(_db.Connection).Options,
        Options.Create(new HostingOptions { Mode = AppMode.Hosted, AdminEmail = Admin, GoogleClientId = "id", GoogleClientSecret = "secret" }),
        new FixedTimeProvider(now ?? ApiFactory.Now));

    private static GoogleIdentity Google(string sub, string email, bool verified = true, string? name = "Some One") =>
        new(sub, email, verified, name, "https://pic/x.png");

    private int UserCount() { using var c = _db.For(1); return c.Users.Count(); }

    private void Invite(string email)
    {
        using var c = _db.For(1);
        c.Invites.Add(new InviteRow { Email = email, CreatedAt = DateTime.UtcNow });
        c.SaveChanges();
    }

    [Fact]
    public async Task NotInvited_Refused_AndNothingIsStored()
    {
        var before = UserCount();
        var r = await Provisioner().SignInAsync(Google("g-1", "stranger@example.com"));
        Assert.Equal(SignInStatus.InviteOnly, r.Status);
        Assert.Equal(before, UserCount());
    }

    [Fact]
    public async Task Invited_CreatesProfile_CaseInsensitive_AndMarksInviteUsed()
    {
        Invite("anna@example.com");
        var r = await Provisioner().SignInAsync(Google("g-anna", "Anna@Example.com", name: "Anna K"));
        Assert.Equal(SignInStatus.Ok, r.Status);
        Assert.False(r.IsAdmin);
        using var c = _db.For(1);
        var u = c.Users.Single(x => x.Id == r.UserId);
        Assert.Equal("g-anna", u.GoogleSubject);
        Assert.Equal("anna@example.com", u.Email);
        Assert.Equal("Anna K", u.DisplayName);
        Assert.Equal(ApiFactory.Now.UtcDateTime, u.LastSignInAt);
        var inv = c.Invites.Single(i => i.Email == "anna@example.com");
        Assert.Equal(r.UserId, inv.UsedByUserId);
        Assert.NotNull(inv.UsedAt);
    }

    [Fact]
    public async Task SecondSignIn_SameAccount_SameUser()
    {
        Invite("bob@example.com");
        var first = await Provisioner().SignInAsync(Google("g-bob", "bob@example.com"));
        var count = UserCount();
        var second = await Provisioner().SignInAsync(Google("g-bob", "bob@example.com", name: "Bob Renamed"));
        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(count, UserCount());
        using var c = _db.For(1);
        Assert.Equal("Bob Renamed", c.Users.Single(x => x.Id == first.UserId).DisplayName);
    }

    [Fact]
    public async Task UnverifiedEmail_Refused_EvenIfInvited()
    {
        Invite("carl@example.com");
        var r = await Provisioner().SignInAsync(Google("g-carl", "carl@example.com", verified: false));
        Assert.Equal(SignInStatus.EmailNotVerified, r.Status);
    }

    [Fact]
    public async Task InviteUsedByAnotherAccount_Refused()
    {
        Invite("dina@example.com");
        Assert.Equal(SignInStatus.Ok, (await Provisioner().SignInAsync(Google("g-dina", "dina@example.com"))).Status);
        var other = await Provisioner().SignInAsync(Google("g-someone-else", "dina@example.com"));
        Assert.Equal(SignInStatus.InviteOnly, other.Status);
    }

    [Fact]
    public async Task Admin_FirstSignIn_TakesOverUser1_WithTheOwnersData_NoInviteNeeded()
    {
        var vehicle = _db.SeedVehicle(1);
        var count = UserCount();
        var r = await Provisioner().SignInAsync(Google("g-owner", "OWNER@example.com", name: "The Owner"));
        Assert.Equal(SignInStatus.Ok, r.Status);
        Assert.True(r.IsAdmin);
        Assert.Equal(1, r.UserId);
        Assert.Equal(count, UserCount());
        using var c = _db.For(1);
        Assert.Equal("g-owner", c.Users.Single(x => x.Id == 1).GoogleSubject);
        Assert.Equal("The Owner", c.Users.Single(x => x.Id == 1).Name);
        Assert.Contains(c.Vehicles.ToList(), v => v.Id == vehicle);
        // and again later: still user 1
        Assert.Equal(1, (await Provisioner().SignInAsync(Google("g-owner", Admin))).UserId);
    }

    [Fact]
    public async Task Blocked_CannotSignIn_ButTheAdminNeverIsBlocked()
    {
        Invite("erik@example.com");
        var r = await Provisioner().SignInAsync(Google("g-erik", "erik@example.com"));
        using (var c = _db.For(1))
        {
            c.Users.Single(u => u.Id == r.UserId).Blocked = true;
            c.SaveChanges();
        }
        Assert.Equal(SignInStatus.Blocked, (await Provisioner().SignInAsync(Google("g-erik", "erik@example.com"))).Status);
        Assert.False((await Provisioner().ValidateAsync(r.UserId)).Valid);

        var admin = await Provisioner().SignInAsync(Google("g-owner", Admin));
        using (var c = _db.For(1))
        {
            c.Users.Single(u => u.Id == admin.UserId).Blocked = true;
            c.SaveChanges();
        }
        Assert.Equal(SignInStatus.Ok, (await Provisioner().SignInAsync(Google("g-owner", Admin))).Status);
        Assert.True((await Provisioner().ValidateAsync(admin.UserId)).Valid);
    }

    [Fact]
    public async Task Validate_UnknownUser_Invalid()
    {
        Assert.False((await Provisioner().ValidateAsync(9999)).Valid);
    }

    public void Dispose() => _db.Dispose();
}

/// <summary>Hosted mode refuses to start when it cannot sign people in.</summary>
public sealed class HostedStartupTests
{
    private sealed class Hosted(params (string Key, string? Value)[] settings) : ApiFactory
    {
        protected override void ConfigureAuth(IServiceCollection s) { }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("DriverBalance:Mode", "Hosted");
            foreach (var (k, v) in settings) if (v is not null) builder.UseSetting(k, v);
        }
    }

    [Fact]
    public void MissingGoogleAndAdmin_RefusesToStart_NamingWhatIsMissing()
    {
        using var f = new Hosted();
        var e = Assert.ThrowsAny<Exception>(() => f.CreateClient());
        var text = e.ToString();
        Assert.Contains("Hosted mode needs", text);
        Assert.Contains("DriverBalance:Google:ClientId", text);
        Assert.Contains("DriverBalance:Google:ClientSecret", text);
        Assert.Contains("DriverBalance:AdminEmail", text);
    }

    [Fact]
    public void UnknownMode_RefusesToStart()
    {
        using var f = new Hosted(("DriverBalance:Mode", "Public"));
        Assert.ThrowsAny<Exception>(() => f.CreateClient());
    }
}

/// <summary>The running hosted app: cookie sessions, API tokens, no local auto sign-in.</summary>
public sealed class HostedAuthApiTests : IClassFixture<HostedAuthApiTests.HostedFactory>
{
    private const string AdminEmail = "owner@example.com";

    public sealed class HostedFactory : ApiFactory
    {
        public AdjustableClock Clock { get; } = new(Now);

        protected override void ConfigureAuth(IServiceCollection s) { }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("DriverBalance:Mode", "Hosted");
            builder.UseSetting("DriverBalance:AdminEmail", AdminEmail);
            builder.UseSetting("DriverBalance:Google:ClientId", "test-client-id");
            builder.UseSetting("DriverBalance:Google:ClientSecret", "test-secret");
            builder.UseSetting("DriverBalance:KeysPath", Path.Combine(Path.GetTempPath(), "db-keys-" + Guid.NewGuid().ToString("N")));
            builder.ConfigureServices(s =>
            {
                s.AddSingleton<TimeProvider>(Clock);
            });
        }
    }

    private readonly HostedFactory _f;

    public HostedAuthApiTests(HostedFactory f) => _f = f;

    private HttpClient NoRedirects() =>
        _f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private long SignIn(string sub, string email, bool invite = true, string? name = "Some One")
    {
        using var scope = _f.Services.CreateScope();
        if (invite && email != AdminEmail)
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DriverBalanceDbContext>>();
            using var db = new DriverBalanceDbContext(options, new FixedUser(0));
            if (!db.Invites.Any(i => i.Email == email)) { db.Invites.Add(new InviteRow { Email = email, CreatedAt = DateTime.UtcNow }); db.SaveChanges(); }
        }
        var r = scope.ServiceProvider.GetRequiredService<UserProvisioner>().SignInAsync(new GoogleIdentity(sub, email, true, name, null)).GetAwaiter().GetResult();
        Assert.Equal(SignInStatus.Ok, r.Status);
        return r.UserId;
    }

    /// <summary>A real session cookie for the user, made with the app's own cookie format (no Google involved).</summary>
    private HttpClient Session(long userId, bool isAdmin, bool freshlyChecked = true)
    {
        var options = _f.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(HostedAuth.Cookie);
        var props = new AuthenticationProperties { IsPersistent = true, IssuedUtc = _f.Clock.GetUtcNow(), ExpiresUtc = _f.Clock.GetUtcNow().AddDays(30) };
        if (freshlyChecked) props.SetString("checked", _f.Clock.GetUtcNow().ToString("o"));
        var ticket = new AuthenticationTicket(HostedAuth.Principal(userId, isAdmin), props, HostedAuth.Cookie);
        var c = NoRedirects();
        c.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}");
        return c;
    }

    [Fact]
    public async Task NotSignedIn_ApiIs401_NotARedirect_AndLoopbackIsNotUser1()
    {
        var c = NoRedirects();
        var r = await c.GetAsync("/api/vehicles");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Null(r.Headers.Location);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task NotSignedIn_StartPage_HasTheStaticLanding()
    {
        var r = await NoRedirects().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("spa-index", html);
        Assert.Contains("LANDING-PAGE", html);
    }

    [Fact]
    public async Task SignedIn_StartPage_LeavesTheLandingOut()
    {
        var user = SignIn("g-landing", "landing@example.com");
        var html = await (await Session(user, isAdmin: false).GetAsync("/trips")).Content.ReadAsStringAsync();
        Assert.Contains("spa-index", html);
        Assert.DoesNotContain("LANDING-PAGE", html);
        Assert.DoesNotContain("<!--landing-->", html);
    }

    [Fact]
    public async Task SignInLink_GoesToGoogle_WithOurClientId_AndTheCallbackPath()
    {
        var r = await NoRedirects().GetAsync("/auth/login?returnUrl=/trips");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var to = r.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/", to);
        Assert.Contains("client_id=test-client-id", to);
        Assert.Contains(Uri.EscapeDataString("/signin-google"), to);
    }

    [Fact]
    public async Task Session_Me_ShowsProfile_AndAdminOnlyForTheAdminEmail()
    {
        var user = SignIn("g-me-1", "me1@example.com", name: "Mia");
        var me = await Api.ReadAsync<MeBody>(await Session(user, isAdmin: false).GetAsync("/api/me"));
        Assert.Equal("Mia", me.Name);
        Assert.Equal("me1@example.com", me.Email);
        Assert.False(me.IsAdmin);
        Assert.Equal("Hosted", me.Mode);

        var admin = SignIn("g-owner", AdminEmail, name: "Owner");
        Assert.True((await Api.ReadAsync<MeBody>(await Session(admin, isAdmin: true).GetAsync("/api/me"))).IsAdmin);
    }

    [Fact]
    public async Task Session_NonAdmin_Is403OnAdminApi()
    {
        var user = SignIn("g-na", "na@example.com");
        var c = Session(user, isAdmin: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/invites")).StatusCode);
    }

    [Fact]
    public async Task Session_ForgedAdminClaim_IsCorrectedAtTheNextCheck()
    {
        var user = SignIn("g-forge", "forge@example.com");
        // a cookie that says admin but was last checked long ago: the server re-reads the truth (not the configured admin email)
        var c = Session(user, isAdmin: true, freshlyChecked: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Session_UserBlockedOrDeleted_IsSignedOut_AtTheNextCheck()
    {
        var blocked = SignIn("g-bl", "bl@example.com");
        var deleted = SignIn("g-del", "del@example.com");
        var live = Session(blocked, isAdmin: false);
        var gone = Session(deleted, isAdmin: false);
        Assert.Equal(HttpStatusCode.OK, (await live.GetAsync("/api/me")).StatusCode);

        using (var scope = _f.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DriverBalanceDbContext>>();
            using var db = new DriverBalanceDbContext(options, new FixedUser(0));
            db.Users.Single(u => u.Id == blocked).Blocked = true;
            db.Users.Where(u => u.Id == deleted).ExecuteDelete();
            db.SaveChanges();
        }
        // inside the 5 minute window nothing is re-checked; after it the sessions are refused
        _f.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(HttpStatusCode.Unauthorized, (await live.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await gone.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Users_OnlySeeTheirOwnData()
    {
        var a = SignIn("g-iso-a", "iso-a@example.com");
        var b = SignIn("g-iso-b", "iso-b@example.com");
        var ca = Session(a, false);
        var cb = Session(b, false);
        var vid = await Api.NewVehicleAsync(ca, "Anna's car");
        Assert.DoesNotContain(await Api.ReadAsync<List<Api.VehicleBody>>(await cb.GetAsync("/api/vehicles")), v => v.Id == vid);
        Assert.Equal(HttpStatusCode.NotFound, (await cb.GetAsync($"/api/vehicles/{vid}")).StatusCode);
    }

    [Fact]
    public async Task ApiToken_Works_AsBearer_ForItsOwnUser_AndIsNeverAdmin()
    {
        var user = SignIn("g-tok", "tok@example.com");
        var token = ApiTokens.Generate();
        using (var scope = _f.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DriverBalanceDbContext>>();
            using var db = new DriverBalanceDbContext(options, new FixedUser(user));
            db.ApiTokens.Add(new ApiTokenRow { Name = "bot", TokenHash = ApiTokens.Hash(token), CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }
        var c = NoRedirects();
        c.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/vehicles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/users")).StatusCode);

        var bad = NoRedirects();
        bad.DefaultRequestHeaders.Add("Authorization", "Bearer " + ApiTokens.Generate());
        Assert.Equal(HttpStatusCode.Unauthorized, (await bad.GetAsync("/api/vehicles")).StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsTheCookie()
    {
        var user = SignIn("g-out", "out@example.com");
        var r = await Session(user, false).PostAsync("/api/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Contains(r.Headers.GetValues("Set-Cookie"), v => v.StartsWith("db_session=;"));
    }

    [Fact]
    public async Task DeleteMyAccount_NeedsTheWord_RemovesEverything_AndTheInvite()
    {
        var user = SignIn("g-bye", "bye@example.com");
        var c = Session(user, false);
        var vid = await Api.NewVehicleAsync(c, "Doomed");
        Directory.CreateDirectory(Path.Combine(_f.DocumentsDir, vid.ToString()));
        File.WriteAllText(Path.Combine(_f.DocumentsDir, vid.ToString(), "x.txt"), "x");

        Assert.Equal(HttpStatusCode.BadRequest, (await c.SendAsync(Delete(new { confirm = "no" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.SendAsync(Delete(new { confirm = "DELETE" }))).StatusCode);

        using var scope = _f.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DriverBalanceDbContext>>();
        using var db = new DriverBalanceDbContext(options, new FixedUser(0));
        Assert.False(db.Users.Any(u => u.Id == user));
        Assert.False(db.Vehicles.IgnoreQueryFilters().Any(v => v.Id == vid));
        Assert.False(db.Invites.Any(i => i.Email == "bye@example.com"));
        Assert.False(Directory.Exists(Path.Combine(_f.DocumentsDir, vid.ToString())));
    }

    [Fact]
    public async Task TheAdmin_CannotDeleteTheirOwnAccount()
    {
        var admin = SignIn("g-owner", AdminEmail);
        var r = await Session(admin, true).SendAsync(Delete(new { confirm = "DELETE" }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    private static HttpRequestMessage Delete(object body) =>
        new(HttpMethod.Delete, "/api/me") { Content = JsonContent.Create(body, options: ApiJson.Options) };

    private sealed record MeBody(long Id, string Name, string? Email, string? PictureUrl, bool IsAdmin, string Mode);
}

/// <summary>Invites and users, as the admin sees them (the X-Test-Admin header gives the admin claim).</summary>
public sealed class AdminApiTests : IClassFixture<AdminApiTests.AdminFactory>
{
    public sealed class AdminFactory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("DriverBalance:AdminEmail", "owner@example.com");
        }
    }

    private readonly AdminFactory _f;

    public AdminApiTests(AdminFactory f)
    {
        _f = f;
        _f.EnsureUser(2);
        _f.EnsureUser(3);
    }

    private HttpClient Admin(long id = 1)
    {
        var c = _f.ClientFor(id);
        c.DefaultRequestHeaders.Add("X-Test-Admin", "1");
        return c;
    }

    private sealed record InviteBody(string Email, DateTime CreatedAt, DateTime? UsedAt, long? UsedByUserId);
    private sealed record UserBody(long Id, string Name, string? Email, DateTime CreatedAt, DateTime? LastSignInAt, int VehicleCount, bool IsAdmin, bool Blocked);

    [Fact]
    public async Task NotAdmin_403()
    {
        var c = _f.ClientFor(2);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/invites")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Api.PostAsync(c, "/api/admin/invites", new { email = "x@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync("/api/admin/users/3")).StatusCode);
    }

    [Fact]
    public async Task Invites_AddNormalised_RejectDuplicatesAndGarbage_RemoveUnused()
    {
        var c = Admin();
        var created = await Api.PostAsync(c, "/api/admin/invites", new { email = "  New.Person@Example.COM " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("new.person@example.com", (await Api.ReadAsync<InviteBody>(created)).Email);

        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, "/api/admin/invites", new { email = "NEW.person@example.com" })).StatusCode);
        foreach (var bad in new[] { "", "nope", "a b@example.com", "@example.com", "x@" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, "/api/admin/invites", new { email = bad })).StatusCode);

        var list = await Api.ReadAsync<List<InviteBody>>(await c.GetAsync("/api/admin/invites"));
        Assert.Contains(list, i => i.Email == "new.person@example.com" && i.UsedAt is null);

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync("/api/admin/invites/new.person@example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync("/api/admin/invites/new.person@example.com")).StatusCode);
    }

    [Fact]
    public async Task RemovingAUsedInvite_BlocksTheUser_KeepsTheirData()
    {
        _f.UseDb(db =>
        {
            db.Users.Single(u => u.Id == 2).Email = "used@example.com";
            db.Invites.Add(new InviteRow { Email = "used@example.com", CreatedAt = DateTime.UtcNow, UsedByUserId = 2, UsedAt = DateTime.UtcNow });
        });
        var vid = await Api.NewVehicleAsync(_f.ClientFor(2), "Kept car");

        Assert.Equal(HttpStatusCode.NoContent, (await Admin().DeleteAsync("/api/admin/invites/used@example.com")).StatusCode);

        var users = await Api.ReadAsync<List<UserBody>>(await Admin().GetAsync("/api/admin/users"));
        var u = users.Single(x => x.Id == 2);
        Assert.True(u.Blocked);
        Assert.Equal(1, u.VehicleCount);
        Assert.Contains(await Api.ReadAsync<List<InviteBody>>(await Admin().GetAsync("/api/admin/invites")), i => i.Email == "used@example.com");
        Assert.Equal(vid, (await Api.ReadAsync<List<Api.VehicleBody>>(await _f.ClientFor(2).GetAsync("/api/vehicles"))).Single(v => v.Name == "Kept car").Id);
    }

    [Fact]
    public async Task DeleteUser_RemovesRows_TyreSetsAndExpenses_Documents_AndInvite_NotOthers()
    {
        _f.UseDb(db =>
        {
            db.Users.Single(u => u.Id == 3).Email = "gone@example.com";
            db.Invites.Add(new InviteRow { Email = "gone@example.com", CreatedAt = DateTime.UtcNow, UsedByUserId = 3 });
        });
        var c3 = _f.ClientFor(3);
        var vid = await Api.NewVehicleAsync(c3, "Doomed");
        var tyre = await c3.PostAsJsonAsync($"/api/vehicles/{vid}/tyres", new { name = "Winter", season = "Winter" }, ApiJson.Options);
        tyre.EnsureSuccessStatusCode();
        var other = await Api.NewVehicleAsync(_f.ClientFor(2), "Someone else's car");
        Directory.CreateDirectory(Path.Combine(_f.DocumentsDir, vid.ToString()));
        File.WriteAllText(Path.Combine(_f.DocumentsDir, vid.ToString(), "x.txt"), "x");

        Assert.Equal(HttpStatusCode.NoContent, (await Admin().DeleteAsync("/api/admin/users/3")).StatusCode);

        _f.UseDb(db =>
        {
            Assert.False(db.Users.Any(u => u.Id == 3));
            Assert.False(db.Vehicles.IgnoreQueryFilters().Any(v => v.Id == vid));
            Assert.False(db.TyreSets.IgnoreQueryFilters().Any(t => t.UserId == 3));
            Assert.False(db.Invites.Any(i => i.Email == "gone@example.com"));
            Assert.True(db.Vehicles.IgnoreQueryFilters().Any(v => v.Id == other));
        });
        Assert.False(Directory.Exists(Path.Combine(_f.DocumentsDir, vid.ToString())));
        Assert.Equal(HttpStatusCode.NotFound, (await Admin().DeleteAsync("/api/admin/users/3")).StatusCode);
    }

    [Fact]
    public async Task DeleteUser_NotYourselfAndNotTheAdminAccount()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin(1).DeleteAsync("/api/admin/users/1")).StatusCode);
        _f.UseDb(db => db.Users.Single(u => u.Id == 2).Email = "owner@example.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin(1).DeleteAsync("/api/admin/users/2")).StatusCode);
        _f.UseDb(db => db.Users.Single(u => u.Id == 2).Email = "user2@example.com");
    }
}
