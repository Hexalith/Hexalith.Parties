using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.Parties.Picker.Tests.Services;

public sealed class PartyPickerTransportGuardrailTests
{
    [Theory]
    [InlineData("api/v1/parties")]
    [InlineData("api/v1/parties/search")]
    [InlineData("api/v1/parties/")]
    [InlineData("HttpClient")]
    [InlineData("GetAsync(")]
    [InlineData("SendAsync(")]
    [InlineData("MarkupString")]
    [InlineData("AddMarkupContent")]
    [InlineData("innerHTML")]
    [InlineData("PartyActor")]
    [InlineData("PartyDetailProjectionActor")]
    [InlineData("PartyIndexProjectionActor")]
    [InlineData("IPartySearchService")]
    [InlineData("Dapr.Actors")]
    [InlineData("Hexalith.Parties.Server")]
    [InlineData("Hexalith.Parties.Projections")]
    [InlineData("localStorage")]
    [InlineData("sessionStorage")]
    [InlineData("indexedDB")]
    [InlineData("document.cookie")]
    public void ProductionPickerSource_DoesNotContainRetiredTransportOrRawMarkupMarkers(string forbidden)
    {
        string combinedSource = string.Join(
            Environment.NewLine,
            GetProductionPickerSourceFiles()
                .Select(path => StripCommentsAndStrings(File.ReadAllText(path), path)));

        combinedSource.ShouldNotContain(forbidden);
    }

    [Theory]
    [InlineData(@"\b(?:globalThis|self|window)\s*\[\s*[""'](?:localStorage|sessionStorage|indexedDB)[""']\s*\]")]
    [InlineData(@"\bdocument\s*\[\s*[""']cookie[""']\s*\]")]
    public void ProductionPickerSource_DoesNotContainStringIndexedBrowserStorageAccess(string forbiddenPattern)
    {
        string combinedSource = string.Join(
            Environment.NewLine,
            GetProductionPickerSourceFiles()
                .Select(path => StripComments(File.ReadAllText(path), path)));

        Regex.IsMatch(combinedSource, forbiddenPattern, RegexOptions.IgnoreCase).ShouldBeFalse();
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

    private static IEnumerable<string> GetProductionPickerSourceFiles()
    {
        string sourceRoot = FindRepositoryRoot();
        string pickerRoot = Path.Combine(sourceRoot, "src", "Hexalith.Parties.Picker");
        return Directory.GetFiles(pickerRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static string StripComments(string source, string path)
    {
        string withoutBlockComments = Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty);
        string withoutLineComments = Regex.Replace(withoutBlockComments, @"//.*$", string.Empty, RegexOptions.Multiline);
        if (!path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            withoutLineComments = Regex.Replace(withoutLineComments, @"^\s*///.*$", string.Empty, RegexOptions.Multiline);
        }

        return withoutLineComments;
    }

    private static string StripCommentsAndStrings(string source, string path)
    {
        if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return StripJsCommentsAndStrings(source);
        }

        string withoutXmlDoc = Regex.Replace(source, @"^\s*///.*$", string.Empty, RegexOptions.Multiline);
        string withoutBlockComments = Regex.Replace(withoutXmlDoc, @"/\*[\s\S]*?\*/", string.Empty);
        string withoutLineComments = Regex.Replace(withoutBlockComments, @"//.*$", string.Empty, RegexOptions.Multiline);
        string withoutVerbatimStrings = Regex.Replace(withoutLineComments, @"@""(?:""""|[^""])*""", "\"\"");
        string withoutInterpolatedStrings = Regex.Replace(withoutVerbatimStrings, @"\$""(?:\\.|[^""\\])*""", "\"\"");
        string withoutRegularStrings = Regex.Replace(withoutInterpolatedStrings, @"""(?:\\.|[^""\\])*""", "\"\"");
        return withoutRegularStrings;
    }

    private static string StripJsCommentsAndStrings(string source)
    {
        string withoutBlockComments = Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty);
        string withoutLineComments = Regex.Replace(withoutBlockComments, @"//.*$", string.Empty, RegexOptions.Multiline);
        string withoutDoubleQuoted = Regex.Replace(withoutLineComments, @"""(?:\\.|[^""\\])*""", "\"\"");
        string withoutSingleQuoted = Regex.Replace(withoutDoubleQuoted, @"'(?:\\.|[^'\\])*'", "''");
        string withoutTemplateLiterals = Regex.Replace(withoutSingleQuoted, @"`(?:\\.|[^`\\])*`", "``");
        return withoutTemplateLiterals;
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
