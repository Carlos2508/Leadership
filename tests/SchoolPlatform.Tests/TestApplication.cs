using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolPlatform.Tests;

public sealed class TestApplication(string environment = "Development",
    Dictionary<string, string?>? configuration = null, Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureServices(services => configureServices?.Invoke(services));
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:SchoolDatabase"] = "Server=localhost;Database=SchoolPlatform_Unused;Integrated Security=true;Encrypt=true;TrustServerCertificate=true",
                ["Logging:LogLevel:Default"] = "Warning"
            }.Concat(configuration ?? []).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Last().Value)));
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost")
    });
}
