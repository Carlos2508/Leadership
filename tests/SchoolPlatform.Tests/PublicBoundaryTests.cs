using System.Net;
using System.Text.Json;

namespace SchoolPlatform.Tests;

public sealed class PublicBoundaryTests
{
    [Fact]
    public async Task Liveness_is_up_while_readiness_reports_an_unreachable_database_without_details()
    {
        await using var app = new TestApplication(configuration: new()
        {
            ["ConnectionStrings:SchoolDatabase"] = "Server=127.0.0.1,1;Database=SchoolPlatform_Unused;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Connect Timeout=2"
        });
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/health/live")).StatusCode);
        var ready = await browser.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Public_configuration_has_branding_without_internal_settings()
    {
        await using var app = new TestApplication(configuration: new()
        {
            ["Institution:Documents:TaxId"] = "PRIVATE-TAX-TEST",
            ["Institution:Contact:Phone"] = "+502 0000 0000"
        });
        using var browser = app.Browser();
        var response = await browser.GetAsync("/api/v1/bootstrap");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("Colegio Leadership Mixco", json.GetProperty("displayName").GetString());
        Assert.Equal("+502 0000 0000", json.GetProperty("contact").GetProperty("phone").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.DoesNotContain("PRIVATE-TAX-TEST", body);
        Assert.DoesNotContain("connectionStrings", body, StringComparison.OrdinalIgnoreCase);
        Assert.False(json.TryGetProperty("schoolId", out _));
        Assert.NotEqual(json.GetProperty("assets").GetProperty("mainLogo").GetString(), json.GetProperty("assets").GetProperty("receiptLogo").GetString());
    }

    [Fact]
    public async Task Changing_institution_configuration_changes_login_theme_and_public_brand()
    {
        await using var app = new TestApplication(configuration: new()
        {
            ["Institution:DisplayName"] = "Colegio de ejemplo",
            ["Institution:ShortName"] = "Mi colegio",
            ["Institution:Public:HeroTitle"] = "Colegio de ejemplo",
            ["Institution:Public:HeroAccent"] = "Ciudad",
            ["Institution:Public:CommunityText"] = "Nuestra comunidad de ejemplo.",
            // Another school also replaces its own contact data (all of it lives only in configuration).
            ["Institution:Contact:MapQuery"] = "Colegio de ejemplo, Ciudad",
            ["Institution:Contact:Email"] = "info@example.test",
            ["Institution:Theme:Primary"] = "#234C6F"
        });
        using var browser = app.Browser();
        var bootstrap = await browser.GetStringAsync("/api/v1/bootstrap");
        Assert.Contains("Colegio de ejemplo", bootstrap);
        Assert.DoesNotContain("Leadership", bootstrap);
        var login = await browser.GetStringAsync("/Identity/Account/Login");
        Assert.Contains("Colegio de ejemplo", login);
        Assert.DoesNotContain("Leadership", login);
        Assert.Contains("#234C6F", await browser.GetStringAsync("/branding/theme.css"));
    }

    [Fact]
    public async Task Production_has_no_sample_data_and_private_api_does_not_redirect_to_html()
    {
        await using var app = new TestApplication("Production");
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/v1/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/v1/does-not-exist")).StatusCode);
        var bootstrap = JsonDocument.Parse(await browser.GetStringAsync("/api/v1/bootstrap")).RootElement;
        Assert.False(bootstrap.GetProperty("previewEnabled").GetBoolean());
    }

    [Fact]
    public async Task Login_rejects_posts_without_csrf_token()
    {
        await using var app = new TestApplication();
        using var browser = app.Browser();
        var response = await browser.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "synthetic", ["Input.Password"] = "SyntheticPassword!123"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Broken_branding_fails_startup_instead_of_using_a_different_client()
    {
        using var app = new TestApplication(configuration: new() { ["Institution:Assets:MainLogo"] = "/missing.png" });
        Assert.ThrowsAny<Exception>(() => app.Browser());
    }
}
