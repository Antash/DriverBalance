using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DriverBalance.Web.Tests.ApiTests;

public sealed class DocumentApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4 fake pdf bytes");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static async Task<long> NewExpenseAsync(HttpClient c, long vid)
    {
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses",
            new { date = "2026-05-01", category = "Maintenance", amount = 2500, currency = "CZK" });
        r.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();
    }

    private static MultipartFormDataContent Form(params (string name, string type, byte[] data)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, type, data) in files)
        {
            var part = new ByteArrayContent(data);
            part.Headers.ContentType = new MediaTypeHeaderValue(type);
            form.Add(part, "files", name);
        }
        return form;
    }

    private static async Task<JsonElement> ExpenseAsync(HttpClient c, long vid, long id)
    {
        var all = JsonDocument.Parse(await c.GetStringAsync($"/api/vehicles/{vid}/expenses")).RootElement;
        return all.EnumerateArray().First(e => e.GetProperty("id").GetInt64() == id).Clone();
    }

    private static async Task<long> FirstIdAsync(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement[0].GetProperty("id").GetInt64();

    private string ExpenseDir(long vid, long eid) => Path.Combine(f.DocumentsDir, vid.ToString(), eid.ToString());

    private async Task<(HttpClient c, long vid, long eid)> SetupAsync()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        return (c, vid, await NewExpenseAsync(c, vid));
    }

    [Fact]
    public async Task Upload_PdfAndPng_AreListedOnTheExpense_AndDownloadReturnsSameBytes()
    {
        var (c, vid, eid) = await SetupAsync();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form(("receipt.pdf", "application/pdf", Pdf), ("photo.png", "image/png", Png)));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var created = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, created.GetArrayLength());

        var docs = (await ExpenseAsync(c, vid, eid)).GetProperty("documents");
        Assert.Equal(2, docs.GetArrayLength());
        var pdf = docs.EnumerateArray().First(d => d.GetProperty("fileName").GetString() == "receipt.pdf");
        Assert.Equal("application/pdf", pdf.GetProperty("contentType").GetString());
        Assert.Equal(Pdf.Length, pdf.GetProperty("sizeBytes").GetInt64());

        var get = await c.GetAsync($"/api/vehicles/{vid}/documents/{pdf.GetProperty("id").GetInt64()}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("application/pdf", get.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Pdf, await get.Content.ReadAsByteArrayAsync());
        Assert.Equal("inline", get.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("receipt.pdf", get.Content.Headers.ContentDisposition!.FileNameStar);
    }

    [Fact]
    public async Task Expense_WithoutDocuments_HasEmptyArray_AndRequestDocumentsAreIgnored()
    {
        var c = f.CreateClient();
        var vid = await Api.NewVehicleAsync(c);
        var r = await Api.PostAsync(c, $"/api/vehicles/{vid}/expenses",
            new { date = "2026-05-01", category = "Fuel", amount = 100, currency = "CZK", documents = new[] { new { id = 5, fileName = "x.pdf", contentType = "application/pdf", sizeBytes = 1 } } });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var body = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(0, body.GetProperty("documents").GetArrayLength());
    }

    [Fact]
    public async Task Delete_RemovesDocument_AndFile()
    {
        var (c, vid, eid) = await SetupAsync();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form(("a.pdf", "application/pdf", Pdf)));
        var docId = await FirstIdAsync(r);
        Assert.Single(Directory.GetFiles(ExpenseDir(vid, eid)));

        var del = await c.DeleteAsync($"/api/vehicles/{vid}/documents/{docId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/vehicles/{vid}/documents/{docId}")).StatusCode);
        Assert.Empty(Directory.Exists(ExpenseDir(vid, eid)) ? Directory.GetFiles(ExpenseDir(vid, eid)) : []);
        Assert.Equal(0, (await ExpenseAsync(c, vid, eid)).GetProperty("documents").GetArrayLength());
    }

    [Fact]
    public async Task DeleteExpense_RemovesRowsAndFolder()
    {
        var (c, vid, eid) = await SetupAsync();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form(("a.pdf", "application/pdf", Pdf)));
        var docId = await FirstIdAsync(r);
        Assert.True(Directory.Exists(ExpenseDir(vid, eid)));

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/vehicles/{vid}/expenses/{eid}")).StatusCode);
        Assert.False(Directory.Exists(ExpenseDir(vid, eid)));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/vehicles/{vid}/documents/{docId}")).StatusCode);
    }

    [Fact]
    public async Task TooLarge_21MB_Is400WithErrors()
    {
        var (c, vid, eid) = await SetupAsync();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form(("big.pdf", "application/pdf", new byte[21 * 1024 * 1024])));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(r)).Errors);
    }

    [Fact]
    public async Task Exactly20MB_IsAccepted()
    {
        var (c, vid, eid) = await SetupAsync();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form(("ok.pdf", "application/pdf", new byte[20 * 1024 * 1024])));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task Exe_Is400_AndNothingIsStored()
    {
        var (c, vid, eid) = await SetupAsync();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents",
            Form(("good.pdf", "application/pdf", Pdf), ("evil.exe", "application/octet-stream", [1, 2, 3])));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(r)).Errors);
        Assert.Equal(0, (await ExpenseAsync(c, vid, eid)).GetProperty("documents").GetArrayLength());
    }

    [Fact]
    public async Task OctetStream_WithKnownExtension_IsAcceptedWithNormalisedType()
    {
        var (c, vid, eid) = await SetupAsync();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form(("IMG_1.JPG", "application/octet-stream", [1, 2, 3])));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement[0];
        Assert.Equal("image/jpeg", doc.GetProperty("contentType").GetString());
    }

    [Fact]
    public async Task NoFiles_AndMalformedMultipart_AreBadRequests()
    {
        var (c, vid, eid) = await SetupAsync();
        var url = $"/api/vehicles/{vid}/expenses/{eid}/documents";
        var none = await c.PostAsync(url, new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(none)).Errors);

        var notMultipart = await c.PostAsJsonAsync(url, new { a = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, notMultipart.StatusCode);

        var bad = new StringContent("--xyz\r\nContent-Disposition: form-data; name=\"files\"; filename=\"a.pdf\"\r\n\r\ntruncated", Encoding.ASCII);
        bad.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=xyz");
        var malformed = await c.PostAsync(url, bad);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.NotEmpty((await Api.ReadAsync<Api.ErrorsBody>(malformed)).Errors);
    }

    [Fact]
    public async Task MoreThanTenFiles_Is400()
    {
        var (c, vid, eid) = await SetupAsync();
        var files = Enumerable.Range(0, 11).Select(i => ($"f{i}.pdf", "application/pdf", Pdf)).ToArray();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form(files));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task PathTraversalName_IsStoredInsideTheExpenseFolder()
    {
        var (c, vid, eid) = await SetupAsync();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form((@"..\..\x.pdf", "application/pdf", Pdf)));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement[0];
        Assert.Equal("x.pdf", doc.GetProperty("fileName").GetString());
        var files = Directory.GetFiles(ExpenseDir(vid, eid));
        Assert.Single(files);
        Assert.EndsWith("-x.pdf", Path.GetFileName(files[0]));
        Assert.False(File.Exists(Path.Combine(f.DocumentsDir, "x.pdf")));
    }

    [Fact]
    public async Task DocumentOfAnotherVehicle_Is404()
    {
        var (c, vid, eid) = await SetupAsync();
        var other = await Api.NewVehicleAsync(c, "Other");
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form(("a.pdf", "application/pdf", Pdf)));
        var docId = await FirstIdAsync(r);

        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/vehicles/{other}/documents/{docId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/vehicles/{other}/documents/{docId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/vehicles/{other}/expenses/{eid}/documents", Form(("a.pdf", "application/pdf", Pdf)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/vehicles/{vid}/documents/{docId}")).StatusCode);
    }

    [Fact]
    public async Task OtherUser_CannotReadDocument()
    {
        var (c, vid, eid) = await SetupAsync();
        var r = await c.PostAsync($"/api/vehicles/{vid}/expenses/{eid}/documents", Form(("a.pdf", "application/pdf", Pdf)));
        var docId = await FirstIdAsync(r);
        f.EnsureUser(7);
        var other = f.ClientFor(7);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/vehicles/{vid}/documents/{docId}")).StatusCode);
    }
}
