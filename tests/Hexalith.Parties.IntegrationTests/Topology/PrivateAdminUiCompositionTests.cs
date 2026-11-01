extern alias adminui;

using System.Net;
using System.Reflection;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Shouldly;

using Composition = adminui::Hexalith.Parties.EventStoreAdminUI.TestHost.PrivateAdminUiComposition;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises actual SDK UI services and HTTP composition; complete catalog/Dapr qualification remains the real topology's responsibility.</summary>
[Collection("Non-parallel")]
public sealed class PrivateAdminUiCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoveryUsesNativeEmptyResponseOrGenuineUnmatchedClientOnly404(bool privateClient)
    {
        WebApplication app = await StartAsync(privateClient).ConfigureAwait(true);
        await using var lifetime = app.ConfigureAwait(true);
        using HttpClient client = app.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");
        using HttpResponseMessage result = await client.GetAsync("/dapr/subscribe", TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.StatusCode.ShouldBe(privateClient ? HttpStatusCode.NotFound : HttpStatusCode.OK);
        result.Headers.Location.ShouldBeNull("Discovery must not become an HTTPS redirect.");
        if (!privateClient) { (await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("[]"); }
    }

    [Fact]
    public async Task PrivateUiServesExactNativeAssetsAndRetainsAuthentication()
    {
        WebApplication app = await StartAsync(privateClient: true).ConfigureAwait(true);
        await using var lifetime = app.ConfigureAwait(true);
        using HttpClient client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        foreach (string relative in new[] { "css/app.css", "js/interop.js", "js/download.js" })
        {
            byte[] body = await client.GetByteArrayAsync("/" + relative, TestContext.Current.CancellationToken).ConfigureAwait(true);
            body.ShouldBe(await File.ReadAllBytesAsync(NativeAssetPath(relative), TestContext.Current.CancellationToken));
            body.ShouldBe(await File.ReadAllBytesAsync(Path.Combine(OutputRoot, "wwwroot", relative), TestContext.Current.CancellationToken));
        }
        using HttpResponseMessage fluent = await client.GetAsync("/_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css", TestContext.Current.CancellationToken).ConfigureAwait(true);
        fluent.StatusCode.ShouldBe(HttpStatusCode.OK);
        fluent.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/css");
        (await fluent.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length.ShouldBeGreaterThan(0);
        using HttpResponseMessage denied = await client.GetAsync("/secured-control", TestContext.Current.CancellationToken).ConfigureAwait(true);
        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PrivateUiRendersTheNativeRazorApplication()
    {
        WebApplication app = await StartAsync(privateClient: true).ConfigureAwait(true);
        await using var lifetime = app.ConfigureAwait(true);
        using HttpClient client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        string page = await client.GetStringAsync("/settings", TestContext.Current.CancellationToken).ConfigureAwait(true);
        page.ShouldContain("Settings - Hexalith EventStore");
        page.ShouldContain("Configure admin dashboard preferences.");
        page.ShouldContain("css/app.css");
        page.ShouldContain("js/interop.js");
        page.ShouldContain("_framework/blazor.web.js");
        page.ShouldNotContain("Something went wrong");
    }

    /// <summary>Resolves the exact native source asset bound by the completed application artifact build.</summary>
    internal static string NativeAssetPath(string relative)
    {
        string row = File.ReadLines(Path.Combine(OutputRoot, "native-admin-ui-artifact.tsv"))
            .Single(line => line.StartsWith("sourceProject|", StringComparison.Ordinal));
        string project = row["sourceProject|".Length..];
        return Path.Combine(Path.GetDirectoryName(project)!, "wwwroot", relative);
    }

    private static string OutputRoot => typeof(Composition).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "PrivateAdminUiOutputRoot").Value!;

    private static async Task<WebApplication> StartAsync(bool privateClient)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Composition).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
            ContentRootPath = OutputRoot,
            WebRootPath = Path.Combine(OutputRoot, "wwwroot"),
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:1",
            ["EventStore:Authentication:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["EventStore:Authentication:Authority"] = string.Empty,
        });
        Composition.AddNativeAdminUiServices(builder);
        WebApplication app = builder.Build();
        if (privateClient) { Composition.UsePrivateAuxiliaryAdminUI(app); }
        else { Composition.UseNativeAdminUi(app); }
        app.MapGet("/secured-control", () => "protected").RequireAuthorization();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return app;
    }
}
