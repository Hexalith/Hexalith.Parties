using Shouldly;

namespace Hexalith.Parties.Picker.Tests.Services;

public sealed class PartyPickerTransportGuardrailTests
{
    [Theory]
    [InlineData("api/v1/parties")]
    [InlineData("api/v1/parties/search")]
    [InlineData("HttpClient")]
    [InlineData("GetAsync(")]
    [InlineData("SendAsync(")]
    [InlineData("MarkupString")]
    [InlineData("AddMarkupContent")]
    [InlineData("innerHTML")]
    public void ProductionPickerSource_DoesNotContainRetiredTransportOrRawMarkupMarkers(string forbidden)
    {
        string sourceRoot = FindRepositoryRoot();
        string pickerRoot = Path.Combine(sourceRoot, "src", "Hexalith.Parties.Picker");
        string combinedSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(pickerRoot, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        combinedSource.ShouldNotContain(forbidden);
    }

    [Fact]
    public void PickerProject_ReferencesTypedPartiesClientButNoForbiddenServerPackages()
    {
        string sourceRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(sourceRoot, "src", "Hexalith.Parties.Picker", "Hexalith.Parties.Picker.csproj");
        string project = File.ReadAllText(projectPath);

        project.ShouldContain("Hexalith.Parties.Client.csproj");
        project.ShouldNotContain("Hexalith.Parties\\Hexalith.Parties.csproj");
        project.ShouldNotContain("Hexalith.Parties.Server");
        project.ShouldNotContain("Hexalith.Parties.Projections");
        project.ShouldNotContain("Dapr.");
        project.ShouldNotContain("MediatR");
        project.ShouldNotContain("FluentValidation");
        project.ShouldNotContain("Microsoft.AspNetCore.Mvc");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Parties.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Hexalith.Parties.slnx from test output directory.");
    }
}
