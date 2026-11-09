using DriverBalance.Core;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class PurchaseCostApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static string Url(long vid, long? id = null) => $"/api/vehicles/{vid}/purchase-costs" + (id is null ? "" : $"/{id}");
    private static Task<HttpResponseMessage> Put(HttpClient c, long vid, object body) => c.PutAsJsonAsync($"/api/vehicles/{vid}", body, ApiJson.Options);

    [Fact]
    public async Task Crud_RoundTrip_TrimmedNote_HomeCurrency()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, Url(vid), new { date = "2025-04-14", amount = 1250000, note = "  Car  " });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var created = await Api.ReadAsync<JsonElement>(r);
        var id = created.GetProperty("id").GetInt64();
        Assert.Equal("Car", created.GetProperty("note").GetString());
        Assert.Equal("2025-04-14", created.GetProperty("date").GetString());

        await Api.PostAsync(c, Url(vid), new { date = "2025-04-13", amount = 2500 });
        var list = await Api.ReadAsync<JsonElement>(await c.GetAsync(Url(vid)));
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(2500, list[0].GetProperty("amount").GetDecimal()); // oldest first
        Assert.Equal(JsonValueKind.Null, list[0].GetProperty("note").ValueKind);

        var u = await c.PutAsJsonAsync(Url(vid, id), new { date = "2025-04-15", amount = 1300000, note = "" }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, u.StatusCode);
        var upd = await Api.ReadAsync<JsonElement>(u);
        Assert.Equal(1300000, upd.GetProperty("amount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, upd.GetProperty("note").ValueKind);

        // They are Purchase expenses in the home currency and show in ownership with the expense id.
        var o = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/ownership"));
        Assert.Equal(1302500, o.GetProperty("purchaseTotal").GetDecimal());
        Assert.Equal(id, o.GetProperty("purchaseCosts").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).Last());
        var exp = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/expenses?category=Purchase"));
        Assert.Equal(2, exp.GetArrayLength());
        Assert.All(exp.EnumerateArray(), e => Assert.Equal("CZK", e.GetProperty("currency").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync(Url(vid, id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync(Url(vid, id))).StatusCode);
        Assert.Equal(1, (await Api.ReadAsync<JsonElement>(await c.GetAsync(Url(vid)))).GetArrayLength());
    }

    [Fact]
    public async Task ExistingSeededPurchaseExpenses_AreListedAndEditable()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        f.SeedExpense(vid, new DateOnly(2025, 4, 14), ExpenseCategory.Purchase, 35000m, "Warranty");
        var list = await Api.ReadAsync<JsonElement>(await c.GetAsync(Url(vid)));
        Assert.Equal("Warranty", list[0].GetProperty("note").GetString());
        var id = list[0].GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync(Url(vid, id), new { date = "2025-04-14", amount = 40000, note = "Warranty" }, ApiJson.Options)).StatusCode);
    }

    [Fact]
    public async Task Validation_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var bad = new Dictionary<string, object>
        {
            ["no date"] = new { amount = 5 },
            ["no amount"] = new { date = "2025-04-14" },
            ["zero"] = new { date = "2025-04-14", amount = 0 },
            ["negative"] = new { date = "2025-04-14", amount = -1 },
            ["long note"] = new { date = "2025-04-14", amount = 1, note = new string('x', 201) },
        };
        foreach (var (name, body) in bad)
        {
            var r = await Api.PostAsync(c, Url(vid), body);
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{name}: {(int)r.StatusCode}");
            Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(r)).Errors);
        }
        var id = (await Api.ReadAsync<JsonElement>(await Api.PostAsync(c, Url(vid), new { date = "2025-04-14", amount = 1 }))).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Url(vid, id), new { date = "2025-04-14", amount = 0 }, ApiJson.Options)).StatusCode);
        Assert.Equal(1, (await Api.ReadAsync<JsonElement>(await c.GetAsync(Url(vid)))).GetArrayLength());
    }

    [Fact]
    public async Task OnlyPurchaseExpenses_AreReachable_AndOtherVehiclesAre404()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var other = await Api.NewVehicleAsync(c, "Other");
        var parking = (await Api.ReadAsync<JsonElement>(await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses",
            new { date = "2025-04-14", category = "Parking", amount = 5, currency = "CZK" }))).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync(Url(vid, parking), new { date = "2025-04-14", amount = 1 }, ApiJson.Options)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync(Url(vid, parking))).StatusCode);

        var mine = (await Api.ReadAsync<JsonElement>(await Api.PostAsync(c, Url(vid), new { date = "2025-04-14", amount = 1 }))).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync(Url(other, mine))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(Url(999999))).StatusCode);
    }

    [Fact]
    public async Task GeneralExpenseEndpoints_StillRefuseNewPurchase()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses", new { date = "2025-04-14", category = "Purchase", amount = 5, currency = "CZK" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var parking = (await Api.ReadAsync<JsonElement>(await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses",
            new { date = "2025-04-14", category = "Parking", amount = 5, currency = "CZK" }))).GetProperty("id").GetInt64();
        var u = await c.PutAsJsonAsync($"/api/vehicles/{vid}/expenses/{parking}", new { date = "2025-04-14", category = "Purchase", amount = 5, currency = "CZK" }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, u.StatusCode);
    }

    [Fact]
    public async Task Financing_RoundTrip_DefaultsMonthly_AndFlowsToOwnership()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Put(c, vid, new { name = "Car", purchaseDate = "2025-04-14", financingStart = "2025-05-01", financingAmount = 9500, financingPayments = 36, financingBalloon = 200000 });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var v = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"));
        Assert.Equal("2025-05-01", v.GetProperty("financingStart").GetString());
        Assert.Equal(9500, v.GetProperty("financingAmount").GetDecimal());
        Assert.Equal("Monthly", v.GetProperty("financingFrequency").GetString());
        Assert.Equal(36, v.GetProperty("financingPayments").GetInt32());
        Assert.Equal(200000, v.GetProperty("financingBalloon").GetDecimal());

        var o = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/ownership"));
        var fin = o.GetProperty("financing");
        Assert.Equal(542000, fin.GetProperty("total").GetDecimal());
        Assert.Equal("2028-04-01", fin.GetProperty("endDate").GetString());
        Assert.Equal(542000, o.GetProperty("purchaseTotal").GetDecimal());
        Assert.Equal(0, o.GetProperty("lumpSumTotal").GetDecimal());

        await Put(c, vid, new { name = "Car", financingStart = "2025-05-01", financingAmount = 1000, financingPayments = 4, financingFrequency = "Quarterly" });
        v = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"));
        Assert.Equal("Quarterly", v.GetProperty("financingFrequency").GetString());
        Assert.Equal(JsonValueKind.Null, v.GetProperty("financingBalloon").ValueKind);

        // All empty = no financing.
        await Put(c, vid, new { name = "Car", financingFrequency = "Yearly", financingBalloon = 5 });
        v = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}"));
        Assert.Equal(JsonValueKind.Null, v.GetProperty("financingStart").ValueKind);
        Assert.Equal(JsonValueKind.Null, v.GetProperty("financingFrequency").ValueKind);
        Assert.Equal(JsonValueKind.Null, v.GetProperty("financingBalloon").ValueKind);
        o = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/ownership"));
        Assert.Equal(JsonValueKind.Null, o.GetProperty("financing").ValueKind);
    }

    [Fact]
    public async Task Financing_Validation_400()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var bad = new Dictionary<string, object>
        {
            ["start only"] = new { name = "Car", financingStart = "2025-05-01" },
            ["amount only"] = new { name = "Car", financingAmount = 100 },
            ["payments only"] = new { name = "Car", financingPayments = 12 },
            ["missing amount"] = new { name = "Car", financingStart = "2025-05-01", financingPayments = 12 },
            ["zero amount"] = new { name = "Car", financingStart = "2025-05-01", financingAmount = 0, financingPayments = 12 },
            ["zero payments"] = new { name = "Car", financingStart = "2025-05-01", financingAmount = 1, financingPayments = 0 },
            ["negative balloon"] = new { name = "Car", financingStart = "2025-05-01", financingAmount = 1, financingPayments = 1, financingBalloon = -1 },
            ["bad frequency"] = new { name = "Car", financingStart = "2025-05-01", financingAmount = 1, financingPayments = 1, financingFrequency = 99 },
        };
        foreach (var (name, body) in bad)
        {
            var r = await Put(c, vid, body);
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{name}: {(int)r.StatusCode}");
            Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(r)).Errors);
        }
        Assert.Equal(HttpStatusCode.OK, (await Put(c, vid, new { name = "Car", financingStart = "2025-05-01", financingAmount = 1, financingPayments = 1, financingBalloon = 0 })).StatusCode);
    }

    [Fact]
    public async Task ForeignCurrency_KeptAsStored_HomeAmountNullWhenUnknown_AndCurrencyValidated()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, Url(vid), new { date = "2025-04-14", amount = 100, currency = "eur" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var created = await Api.ReadAsync<JsonElement>(r);
        var id = created.GetProperty("id").GetInt64();
        Assert.Equal("EUR", created.GetProperty("currency").GetString());
        Assert.Equal(100, created.GetProperty("amount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("homeAmount").ValueKind);

        // Editing without a currency keeps the row's currency.
        var u = await Api.ReadAsync<JsonElement>(await c.PutAsJsonAsync(Url(vid, id), new { date = "2025-04-14", amount = 120 }, ApiJson.Options));
        Assert.Equal("EUR", u.GetProperty("currency").GetString());

        var home = await Api.ReadAsync<JsonElement>(await Api.PostAsync(c, Url(vid), new { date = "2025-04-14", amount = 5 }));
        Assert.Equal("CZK", home.GetProperty("currency").GetString());
        Assert.Equal(5, home.GetProperty("homeAmount").GetDecimal());

        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, Url(vid), new { date = "2025-04-14", amount = 1, currency = "EURO" })).StatusCode);
        // The foreign row without a home amount is not counted in ownership, like before.
        var o = await Api.ReadAsync<JsonElement>(await c.GetAsync($"/api/vehicles/{vid}/ownership"));
        Assert.Equal(5, o.GetProperty("purchaseTotal").GetDecimal());
    }

    [Fact]
    public async Task Amounts_AreCapped_SoOwnershipCannotOverflow()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.PostAsync(c, Url(vid), new { date = "2025-04-14", amount = 2e12 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, vid, new { name = "Car", financingStart = "2025-05-01", financingAmount = 2e12, financingPayments = 600 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, vid, new { name = "Car", financingStart = "2025-05-01", financingAmount = 1, financingPayments = 1, financingBalloon = 2e12 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Put(c, vid, new { name = "Car", financingStart = "2025-05-01", financingAmount = 1e12, financingPayments = 600, financingBalloon = 1e12 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/vehicles/{vid}/ownership")).StatusCode);
    }
}
