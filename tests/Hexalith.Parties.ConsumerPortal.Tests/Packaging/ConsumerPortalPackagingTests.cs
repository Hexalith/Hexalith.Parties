using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Packaging;

public sealed class ConsumerPortalPackagingTests
{
    private static readonly string[] SourceFileExtensions =
    [
        ".cs",
        ".razor",
    ];

    private static readonly string[] StyleFileExtensions =
    [
        ".css",
    ];

    private static readonly string[] ForbiddenColorTokens =
    [
        "#",
        "rgb(",
        "rgba(",
        "hsl(",
        "hsla(",
    ];

    [Fact]
    public void ConsumerPortalProject_UsesRazorSdk_AndExpectedPackageMetadata()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj"));
        XElement root = project.Root.ShouldNotBeNull();

        root.Attribute("Sdk")?.Value.ShouldBe("Microsoft.NET.Sdk.Razor");
        project.Descendants("PackageId").Single().Value.ShouldBe("Hexalith.Parties.ConsumerPortal");
        project.Descendants("Description").Single().Value.ShouldContain("consumer portal");
        project.Descendants("TargetFramework").ShouldBeEmpty();
        project.Descendants("Nullable").ShouldBeEmpty();
        project.Descendants("ImplicitUsings").ShouldBeEmpty();
        project.Descendants("TreatWarningsAsErrors").ShouldBeEmpty();
    }

    [Fact]
    public void ConsumerPortalProject_ReferencesOnlyAllowedAdopterFacingProjects()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj"));

        project.Descendants("FrameworkReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .ShouldContain("Microsoft.AspNetCore.App");

        project.Descendants("PackageReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .ShouldContain("Microsoft.FluentUI.AspNetCore.Components");

        IReadOnlyCollection<string> references = project.Descendants("ProjectReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .ToList();

        references.ShouldContain(@"..\Hexalith.Parties.Client\Hexalith.Parties.Client.csproj");
        references.ShouldContain(@"..\Hexalith.Parties.Contracts\Hexalith.Parties.Contracts.csproj");
        references.ShouldContain(@"$(HexalithFrontComposerRoot)\src\Hexalith.FrontComposer.Shell\Hexalith.FrontComposer.Shell.csproj");

        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.UI", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Server", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Projections", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Security", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Testing", StringComparison.Ordinal));
    }

    [Fact]
    public void ConsumerPortalSource_DoesNotUseFutureDataWorkflows()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");

        string allSource = string.Join(Environment.NewLine, ReadProjectFiles(sourceRoot, SourceFileExtensions)
            .Select(static file => File.ReadAllText(file.AbsolutePath)));

        allSource.ShouldNotContain("ListPartiesAsync", Case.Sensitive);
        allSource.ShouldNotContain("SearchPartiesAsync", Case.Sensitive);
        allSource.ShouldNotContain("ISelfScopedPartiesClient", Case.Sensitive);
        allSource.ShouldContain("IConsumerProfileDataClient", Case.Sensitive);
        allSource.ShouldNotContain("GetPartyAsync", Case.Sensitive);
        allSource.ShouldNotContain("IPartiesQueryClient", Case.Sensitive);
        allSource.ShouldNotContain("IAdminPortalGdprClient", Case.Sensitive);
        allSource.ShouldNotContain("Hexalith.Parties.UI", Case.Sensitive);
        allSource.ShouldContain("IConsumerProfileEditClient", Case.Sensitive);
        allSource.ShouldContain("IConsumerConsentClient", Case.Sensitive);
        allSource.ShouldContain("IConsumerPrivacyExportClient", Case.Sensitive);
        allSource.ShouldContain("IConsumerPrivacyErasureClient", Case.Sensitive);
        allSource.ShouldContain("IConsumerPrivacyProcessingClient", Case.Sensitive);
        allSource.ShouldNotContain("IPartiesCommandClient", Case.Sensitive);
    }

    [Fact]
    public void ConsumerProfileDataPort_DoesNotAcceptCallerSuppliedPartyIds()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string portSource = File.ReadAllText(Path.Combine(sourceRoot, "Services", "IConsumerProfileDataClient.cs"));

        portSource.ShouldContain("GetMyPartyAsync", Case.Sensitive);
        portSource.ShouldNotContain("partyId", Case.Insensitive);
        portSource.ShouldNotContain("GetPartyAsync", Case.Sensitive);
    }

    [Fact]
    public void ConsumerProfileEditPort_DoesNotAcceptCallerSuppliedPartyIds()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string portSource = File.ReadAllText(Path.Combine(sourceRoot, "Services", "IConsumerProfileEditClient.cs"));
        string requestSource = File.ReadAllText(Path.Combine(sourceRoot, "Services", "ConsumerProfileUpdateRequest.cs"));

        portSource.ShouldContain("UpdateMyProfileAsync", Case.Sensitive);
        (portSource + requestSource).ShouldNotContain("partyId", Case.Insensitive);
        (portSource + requestSource).ShouldNotContain("UpdatePartyComposite", Case.Sensitive);
    }

    [Fact]
    public void ConsumerConsentPort_DoesNotExposeCallerSuppliedIdentityOrListSearch()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string serviceSource = string.Join(Environment.NewLine, Directory.GetFiles(Path.Combine(sourceRoot, "Services"), "*Consent*.cs")
            .Select(File.ReadAllText));

        serviceSource.ShouldContain("GetMyConsentOverviewAsync", Case.Sensitive);
        serviceSource.ShouldContain("GrantMyConsentAsync", Case.Sensitive);
        serviceSource.ShouldContain("WithdrawMyConsentAsync", Case.Sensitive);
        serviceSource.ShouldNotContain("partyId", Case.Insensitive);
        serviceSource.ShouldNotContain("PagedResult", Case.Sensitive);
        serviceSource.ShouldNotContain("ListParties", Case.Sensitive);
        serviceSource.ShouldNotContain("SearchParties", Case.Sensitive);
        serviceSource.ShouldNotContain("ListMy", Case.Sensitive);
        serviceSource.ShouldNotContain("SearchMy", Case.Sensitive);
        serviceSource.ShouldNotContain("IPartiesQueryClient", Case.Sensitive);
        serviceSource.ShouldNotContain("IAdminPortalGdprClient", Case.Sensitive);
        serviceSource.ShouldNotContain("ISelfScopedPartiesClient", Case.Sensitive);
    }

    [Fact]
    public void ConsumerPrivacyExportPort_DoesNotExposeCallerSuppliedIdentityOrListSearch()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string serviceSource = string.Join(Environment.NewLine, Directory.GetFiles(Path.Combine(sourceRoot, "Services"), "*Privacy*Export*.cs")
            .Select(File.ReadAllText));

        serviceSource.ShouldContain("ExportMyDataAsync", Case.Sensitive);
        serviceSource.ShouldNotContain("partyId", Case.Insensitive);
        serviceSource.ShouldNotContain("tenantId", Case.Insensitive);
        serviceSource.ShouldNotContain("correlationId", Case.Insensitive);
        serviceSource.ShouldNotContain("PagedResult", Case.Sensitive);
        serviceSource.ShouldNotContain("ListParties", Case.Sensitive);
        serviceSource.ShouldNotContain("SearchParties", Case.Sensitive);
        serviceSource.ShouldNotContain("GetPartyAsync", Case.Sensitive);
        serviceSource.ShouldNotContain("IPartiesQueryClient", Case.Sensitive);
        serviceSource.ShouldNotContain("IAdminPortalGdprClient", Case.Sensitive);
        serviceSource.ShouldNotContain("ISelfScopedPartiesClient", Case.Sensitive);
    }

    [Fact]
    public void ConsumerPrivacyErasurePort_DoesNotExposeCallerSuppliedIdentityOrListSearch()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string serviceSource = string.Join(Environment.NewLine, Directory.GetFiles(Path.Combine(sourceRoot, "Services"), "*Privacy*Erasure*.cs")
            .Select(File.ReadAllText));

        serviceSource.ShouldContain("GetMyErasureStatusAsync", Case.Sensitive);
        serviceSource.ShouldContain("RequestMyErasureAsync", Case.Sensitive);
        serviceSource.ShouldContain("CancelMyErasureAsync", Case.Sensitive);
        serviceSource.ShouldNotContain("partyId", Case.Insensitive);
        serviceSource.ShouldNotContain("tenantId", Case.Insensitive);
        serviceSource.ShouldNotContain("correlationId", Case.Insensitive);
        serviceSource.ShouldNotContain("PagedResult", Case.Sensitive);
        serviceSource.ShouldNotContain("ListParties", Case.Sensitive);
        serviceSource.ShouldNotContain("SearchParties", Case.Sensitive);
        serviceSource.ShouldNotContain("GetPartyAsync", Case.Sensitive);
        serviceSource.ShouldNotContain("IPartiesQueryClient", Case.Sensitive);
        serviceSource.ShouldNotContain("IAdminPortalGdprClient", Case.Sensitive);
        serviceSource.ShouldNotContain("ISelfScopedPartiesClient", Case.Sensitive);
    }

    [Fact]
    public void ConsumerPrivacyProcessingPort_DoesNotExposeCallerSuppliedIdentityOrListSearch()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string serviceSource = string.Join(Environment.NewLine, Directory.GetFiles(Path.Combine(sourceRoot, "Services"), "*Privacy*Processing*.cs")
            .Select(File.ReadAllText));

        serviceSource.ShouldContain("GetMyProcessingSummaryAsync", Case.Sensitive);
        serviceSource.ShouldNotContain("partyId", Case.Insensitive);
        serviceSource.ShouldNotContain("tenantId", Case.Insensitive);
        serviceSource.ShouldNotContain("actorId", Case.Insensitive);
        serviceSource.ShouldNotContain("correlationId", Case.Insensitive);
        serviceSource.ShouldNotContain("PagedResult", Case.Sensitive);
        serviceSource.ShouldNotContain("ListParties", Case.Sensitive);
        serviceSource.ShouldNotContain("SearchParties", Case.Sensitive);
        serviceSource.ShouldNotContain("GetPartyAsync", Case.Sensitive);
        serviceSource.ShouldNotContain("IPartiesQueryClient", Case.Sensitive);
        serviceSource.ShouldNotContain("IAdminPortalGdprClient", Case.Sensitive);
        serviceSource.ShouldNotContain("ISelfScopedPartiesClient", Case.Sensitive);
    }

    [Theory]
    [InlineData("MyProfilePage.razor")]
    [InlineData("EditMyProfilePage.razor")]
    [InlineData("MyConsentPage.razor")]
    [InlineData("MyPrivacyPage.razor")]
    public void ConsumerProfilePages_DoNotUseLoggingOrTelemetryApis(string fileName)
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string profileSource = File.ReadAllText(Path.Combine(sourceRoot, "Components", fileName));

        profileSource.ShouldNotContain("ILogger", Case.Sensitive);
        profileSource.ShouldNotContain("Console.", Case.Sensitive);
        profileSource.ShouldNotContain("Debug.", Case.Sensitive);
        profileSource.ShouldNotContain("ActivitySource", Case.Sensitive);
        profileSource.ShouldNotContain("Meter", Case.Sensitive);
    }

    [Fact]
    public void EditMyProfilePage_UsesOneVisibleSaveStatusSource()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string editSource = File.ReadAllText(Path.Combine(sourceRoot, "Components", "EditMyProfilePage.razor"));

        editSource.ShouldContain("_saveStatusMessage", Case.Sensitive);
        editSource.ShouldContain("role=\"status\"", Case.Sensitive);
        editSource.ShouldNotContain("Toast", Case.Sensitive);
        editSource.ShouldNotContain("ConsumerRouteShell", Case.Sensitive);
    }

    [Fact]
    public void MyConsentPage_UsesOneVisibleSaveStatusSourceAndNoRawIdentifiers()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string source = File.ReadAllText(Path.Combine(sourceRoot, "Components", "MyConsentPage.razor"));

        source.ShouldContain("_statusMessage", Case.Sensitive);
        source.ShouldContain("role=\"status\"", Case.Sensitive);
        source.ShouldContain("role=\"alert\"", Case.Sensitive);
        source.ShouldContain("FluentSwitch", Case.Sensitive);
        source.ShouldNotContain("Toast", Case.Sensitive);
        source.ShouldNotContain("ConsumerRouteShell", Case.Sensitive);
        source.ShouldNotContain("Console.", Case.Sensitive);
        source.ShouldNotContain("ILogger", Case.Sensitive);
        source.ShouldNotContain("correlation", Case.Insensitive);
    }

    [Fact]
    public void MyPrivacyPage_UsesOneVisibleExportStatusSourceAndNoRawIdentifiers()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string source = File.ReadAllText(Path.Combine(sourceRoot, "Components", "MyPrivacyPage.razor"));

        source.ShouldContain("StatusMessage", Case.Sensitive);
        source.ShouldContain("ProcessingStatusMessage", Case.Sensitive);
        source.ShouldContain("role=\"status\"", Case.Sensitive);
        source.ShouldContain("role=\"alert\"", Case.Sensitive);
        source.ShouldContain("DotNetStreamReference", Case.Sensitive);
        source.ShouldContain("href=\"/me/consent\"", Case.Sensitive);
        source.ShouldNotContain("Toast", Case.Sensitive);
        source.ShouldNotContain("ConsumerRouteShell", Case.Sensitive);
        source.ShouldNotContain("Console.", Case.Sensitive);
        source.ShouldNotContain("ILogger", Case.Sensitive);
        source.ShouldNotContain("partyId", Case.Insensitive);
        source.ShouldNotContain("tenantId", Case.Insensitive);
        source.ShouldNotContain("correlation", Case.Insensitive);
    }

    [Fact]
    public void ConsumerPortalStyles_UseDesignTokens_NotRawColorLiterals()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");

        foreach ((string RelativePath, string AbsolutePath) file in ReadProjectFiles(sourceRoot, StyleFileExtensions))
        {
            string source = File.ReadAllText(file.AbsolutePath);

            foreach (string forbidden in ForbiddenColorTokens)
            {
                source.ShouldNotContain(forbidden, Case.Insensitive, $"Forbidden color literal '{forbidden}' found in {file.RelativePath}.");
            }

            source.ShouldNotContain("#0097A7", Case.Insensitive, $"Raw brand teal found in {file.RelativePath}.");
        }
    }

    [Fact]
    public void ConsumerPortalComponents_UseResourceLabelWrapper_ForRegulatedCopy()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string resourcesRoot = Path.Combine(sourceRoot, "Resources");

        Directory.Exists(resourcesRoot).ShouldBeTrue();
        Directory.GetFiles(resourcesRoot, "*.resx", SearchOption.TopDirectoryOnly).ShouldNotBeEmpty();

        foreach (string component in Directory.GetFiles(Path.Combine(sourceRoot, "Components"), "*.razor", SearchOption.TopDirectoryOnly))
        {
            string source = File.ReadAllText(component);

            source.ShouldContain("ConsumerPortalLabels.");
            source.ShouldNotContain("under Article", Case.Insensitive);
            source.ShouldNotContain("within 30 days", Case.Insensitive);
        }
    }

    private static string ProjectRoot(string relativePath)
    {
        string current = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(current, "Hexalith.Parties.slnx")))
        {
            DirectoryInfo? parent = Directory.GetParent(current);
            parent.ShouldNotBeNull();
            current = parent.FullName;
        }

        return Path.Combine(current, relativePath);
    }

    private static IEnumerable<(string RelativePath, string AbsolutePath)> ReadProjectFiles(
        string sourceRoot,
        IReadOnlyCollection<string> extensions)
    {
        if (!Directory.Exists(sourceRoot))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceRoot, file);
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
                || segments.Contains("obj", StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return (relativePath, file);
        }
    }
}
