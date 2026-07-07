using System.Text;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class RetiredLeafProjectFitnessTests
{
    private static readonly string[] RetiredProductionProjectPaths =
    [
        "src/Hexalith.Parties.Server/Hexalith.Parties.Server.csproj",
        "src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj",
    ];

    [Fact]
    public void RetiredProductionProjectPaths_AreAbsentFromSolutionAndProjectReferences()
    {
        string root = RepositoryRoot.Locate();

        Directory.Exists(Path.Combine(root, "src", "Hexalith.Parties.Server")).ShouldBeFalse();
        Directory.Exists(Path.Combine(root, "src", "Hexalith.Parties.ServiceDefaults")).ShouldBeFalse();

        string solution = File.ReadAllText(Path.Combine(root, "Hexalith.Parties.slnx"))
            .Replace('\\', '/');
        foreach (string retiredPath in RetiredProductionProjectPaths)
        {
            solution.ShouldNotContain(retiredPath);
        }

        string[] projectFiles =
        [
            .. new[] { "src", "tests" }
                .Select(static relativePath => Path.Combine(RepositoryRoot.Locate(), relativePath))
                .SelectMany(static path => Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories))
                .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)),
        ];

        foreach (string projectFile in projectFiles)
        {
            XDocument project = XDocument.Load(projectFile);
            string[] references =
            [
                .. project.Descendants()
                    .Where(static element => element.Name.LocalName is "ProjectReference" or "PackageReference")
                    .Select(static element => element.Attribute("Include")?.Value.Replace('\\', '/'))
                    .Where(static include => !string.IsNullOrWhiteSpace(include))
                    .Select(static include => include!),
            ];

            references.ShouldNotContain(reference => reference.Contains("Hexalith.Parties.Server/Hexalith.Parties.Server.csproj", StringComparison.Ordinal));
            references.ShouldNotContain(reference => reference.Contains("Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj", StringComparison.Ordinal));
            references.ShouldNotContain("Hexalith.Parties.ServiceDefaults");
        }
    }

    [Fact]
    public void UiAndMcpHosts_UseCommonsServiceDefaultsDirectlyWithPartiesOptions()
    {
        string root = RepositoryRoot.Locate();
        string[] hostPrograms =
        [
            "src/Hexalith.Parties.UI/Program.cs",
            "src/Hexalith.Parties.Mcp/Program.cs",
        ];

        foreach (string hostProgram in hostPrograms)
        {
            string source = StripComments(File.ReadAllText(Path.Combine(root, hostProgram)));

            source.ShouldContain("using Hexalith.Commons.ServiceDefaults;");
            source.ShouldContain("AddHexalithServiceDefaults(ConfigurePartiesServiceDefaults)");
            source.ShouldContain("MapHexalithDefaultEndpoints(ConfigurePartiesServiceDefaults)");
            source.ShouldContain("options.HealthEndpointPath = \"/health\";");
            source.ShouldContain("options.LivenessEndpointPath = \"/alive\";");
            source.ShouldContain("options.ReadinessEndpointPath = \"/ready\";");
            source.ShouldContain("options.RegisterDefaultSelfCheck = false;");
            source.ShouldContain("options.ActivitySourceNames.Add(\"Hexalith.Parties\");");
            source.ShouldNotContain("Hexalith.Parties.ServiceDefaults");
        }
    }

    [Fact]
    public void PartiesHost_UsesEventStoreDomainServiceSdkAfterStory85Cutover()
    {
        string source = StripComments(File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src/Hexalith.Parties/Program.cs")));

        source.ShouldContain("AddEventStoreDomainService(typeof(PartyAggregate).Assembly)");
        source.ShouldContain("UseEventStoreDomainService()");
        source.ShouldContain("ConfigureOpenTelemetryTracerProvider");
        source.ShouldContain("ConfigureOpenTelemetryMeterProvider");
        source.ShouldContain("Hexalith.Parties");
        source.ShouldNotContain("Hexalith.Parties.ServiceDefaults");
        source.ShouldNotContain("AddHexalithServiceDefaults(ConfigurePartiesServiceDefaults)");
        source.ShouldNotContain("MapHexalithDefaultEndpoints(ConfigurePartiesServiceDefaults)");
    }

    [Fact]
    public void AuthenticationProject_RemainsUntilTenantClaimsPrerequisiteGateIsSatisfied()
    {
        string root = RepositoryRoot.Locate();

        File.Exists(Path.Combine(root, "src", "Hexalith.Parties.Authentication", "Hexalith.Parties.Authentication.csproj")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(root, "Hexalith.Parties.slnx"))
            .ShouldContain("src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj");

        MatrixRow tenantClaimsRow = ReadMatrixRow(
            File.ReadAllText(Path.Combine(
                root,
                "_bmad-output",
                "implementation-artifacts",
                "story-8-3-platform-api-prerequisite-matrix.md")),
            "Tenant claims transformation");
        tenantClaimsRow.Status.ShouldBe("needs-additive-api");
        tenantClaimsRow.EvidencePaths.ShouldContain("src/Hexalith.Parties.Authentication/PartiesClaimsTransformation.cs");
        tenantClaimsRow.Decision.ShouldContain("Hexalith.Parties.Authentication");
        tenantClaimsRow.Decision.ShouldContain("remains after Story 8.4");
    }

    private static string StripComments(string source)
    {
        StringBuilder result = new(source.Length);
        bool inString = false;
        bool inVerbatimString = false;
        bool inCharacter = false;

        for (int i = 0; i < source.Length; i++)
        {
            char current = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (!inString && !inVerbatimString && !inCharacter && current == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }

                if (i < source.Length)
                {
                    _ = result.Append(source[i]);
                }

                continue;
            }

            if (!inString && !inVerbatimString && !inCharacter && current == '/' && next == '*')
            {
                i += 2;
                while (i < source.Length - 1 && (source[i] != '*' || source[i + 1] != '/'))
                {
                    i++;
                }

                i++;
                continue;
            }

            _ = result.Append(current);

            if (inVerbatimString)
            {
                if (current == '"' && next == '"')
                {
                    _ = result.Append(next);
                    i++;
                }
                else if (current == '"')
                {
                    inVerbatimString = false;
                }

                continue;
            }

            if (inString)
            {
                if (current == '\\' && next != '\0')
                {
                    _ = result.Append(next);
                    i++;
                }
                else if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (inCharacter)
            {
                if (current == '\\' && next != '\0')
                {
                    _ = result.Append(next);
                    i++;
                }
                else if (current == '\'')
                {
                    inCharacter = false;
                }

                continue;
            }

            if (current == '@' && next == '"')
            {
                _ = result.Append(next);
                i++;
                inVerbatimString = true;
            }
            else if (current == '"')
            {
                inString = true;
            }
            else if (current == '\'')
            {
                inCharacter = true;
            }
        }

        return result.ToString();
    }

    private static MatrixRow ReadMatrixRow(string matrix, string surface)
    {
        foreach (string line in matrix.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = SplitMarkdownTableRow(trimmed);
            if (IsMarkdownSeparatorRow(cells)
                || cells.Length == 8 && string.Equals(cells[0], "Surface", StringComparison.Ordinal))
            {
                continue;
            }

            if (cells.Length == 8 && string.Equals(cells[0], surface, StringComparison.Ordinal))
            {
                return new MatrixRow(
                    cells[0],
                    cells[1],
                    cells[2],
                    cells[3],
                    cells[4],
                    cells[5],
                    cells[6],
                    cells[7]);
            }
        }

        throw new InvalidOperationException($"Matrix row '{surface}' was not found.");
    }

    private static bool IsMarkdownSeparatorRow(string[] cells)
        => cells.All(static cell =>
            cell.Length > 0 &&
            cell.All(static character => character is '-' or ':' or ' '));

    private static string[] SplitMarkdownTableRow(string row)
    {
        List<string> cells = [];
        int startIndex = row.StartsWith('|') ? 1 : 0;
        int endIndex = row.EndsWith("|", StringComparison.Ordinal) ? row.Length - 1 : row.Length;
        StringBuilder current = new();
        bool escaped = false;
        bool inCodeSpan = false;

        for (int i = startIndex; i < endIndex; i++)
        {
            char currentChar = row[i];
            if (escaped)
            {
                if (currentChar is '|' or '`' or '\\')
                {
                    _ = current.Append(currentChar);
                }
                else
                {
                    _ = current.Append('\\').Append(currentChar);
                }

                escaped = false;
                continue;
            }

            if (currentChar == '\\')
            {
                escaped = true;
                continue;
            }

            if (currentChar == '`')
            {
                inCodeSpan = !inCodeSpan;
                _ = current.Append(currentChar);
                continue;
            }

            if (currentChar == '|' && !inCodeSpan)
            {
                cells.Add(current.ToString().Trim());
                _ = current.Clear();
                continue;
            }

            _ = current.Append(currentChar);
        }

        if (escaped)
        {
            _ = current.Append('\\');
        }

        cells.Add(current.ToString().Trim());
        return [.. cells];
    }
}
