using Bunit;

using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.Parties.UI.Components.Specimens;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class PartiesAccessibilitySpecimenTests : BunitContext
{
    public PartiesAccessibilitySpecimenTests()
    {
        Services.AddLogging();
        Services.AddFluentUIComponents();
        Services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(PartiesUiDomainMarker).Assembly));
        Services.AddHexalithDomain<PartiesUiDomainMarker>();
        JSInterop.SetupVoid(
            "Microsoft.FluentUI.Blazor.Utilities.Attributes.observeAttributeChange",
            _ => true);
    }

    [Theory]
    [InlineData("true", "Development", true)]
    [InlineData("true", "Test", true)]
    [InlineData("true", "Production", false)]
    [InlineData("false", "Development", false)]
    [InlineData("", "Development", false)]
    public void IsEnabled_requires_explicit_flag_and_safe_environment(string flag, string environmentName, bool expected)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PartiesAccessibilitySpecimenRoutes.EnabledConfigurationKey] = flag,
            })
            .Build();
        var environment = new TestWebHostEnvironment { EnvironmentName = environmentName };

        PartiesAccessibilitySpecimenRoutes.IsEnabled(configuration, environment).ShouldBe(expected);
    }

    [Fact]
    public void Enabled_specimen_renders_ready_marker_and_representative_components()
    {
        AddSpecimenServices(enabled: true, environmentName: "Test");

        IRenderedComponent<PartiesAccessibilitySpecimen> cut = Render<PartiesAccessibilitySpecimen>();

        cut.Find("[data-testid='parties-accessibility-specimen-ready']").TextContent.ShouldContain("ready");
        cut.Find("h1").TextContent.ShouldBe("Parties accessibility specimen");
        cut.Find("[data-testid='parties-specimen-primary-action']").ShouldNotBeNull();
        cut.Find("[data-testid='parties-specimen-link']").ShouldNotBeNull();
        cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Synthetic status update");
        cut.Find(".data-freshness-indicator").ShouldNotBeNull();
        cut.Find(".party-state-badge").ShouldNotBeNull();
        cut.Find(".gdpr-destructive-button").ShouldNotBeNull();
    }

    [Fact]
    public void Disabled_specimen_renders_no_route_specific_content_or_test_markers()
    {
        AddSpecimenServices(enabled: false, environmentName: "Test");

        IRenderedComponent<PartiesAccessibilitySpecimen> cut = Render<PartiesAccessibilitySpecimen>();

        cut.FindAll("[data-testid]").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("Parties accessibility specimen");
        cut.Markup.ShouldNotContain("Synthetic shell accessibility surface ready.");
    }

    private void AddSpecimenServices(bool enabled, string environmentName)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PartiesAccessibilitySpecimenRoutes.EnabledConfigurationKey] = enabled ? "true" : "false",
            })
            .Build();

        Services.AddSingleton(configuration);
        Services.AddSingleton<IConfiguration>(configuration);
        Services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment { EnvironmentName = environmentName });
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Hexalith.Parties.UI.Tests";

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
