using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.Parties.PackageTests;

/// <summary>Reads live dependency versions using the repository's actual MSBuild import and condition order.</summary>
internal static class EffectivePackageVersions
{
    private const string DefaultProject = "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj";

    /// <summary>Evaluates one concrete version property in the selected package configuration.</summary>
    internal static string ReadProperty(string repositoryRoot, string propertyName, string projectRelativePath = DefaultProject, string configuration = "Release")
    {
        if (!Regex.IsMatch(propertyName, @"\A[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("An MSBuild property name is required.", nameof(propertyName));
        }

        string value = Query(repositoryRoot, projectRelativePath, $"-getProperty:{propertyName}", configuration);
        // MSBuild appends one line ending; any additional CR/LF belongs to the value and is invalid.
        value = value.EndsWith("\r\n", StringComparison.Ordinal) ? value[..^2]
            : value.EndsWith('\n') ? value[..^1] : value;
        return ValidateVersion(value, propertyName);
    }

    /// <summary>Evaluates central package items after all applicable root pins, imports, and assignments.</summary>
    internal static IReadOnlyDictionary<string, string> ReadPackages(string repositoryRoot, string projectRelativePath = DefaultProject, string configuration = "Release")
    {
        string output = Query(repositoryRoot, projectRelativePath, "-getItem:PackageVersion", configuration);
        using JsonDocument document = JsonDocument.Parse(output);
        return document.RootElement.GetProperty("Items").GetProperty("PackageVersion").EnumerateArray()
            .ToDictionary(
                static item => item.GetProperty("Identity").GetString()!,
                static item => ValidateVersion(item.GetProperty("Version").GetString() ?? string.Empty, item.GetProperty("Identity").GetString()!),
                StringComparer.Ordinal);
    }

    private static string Query(string repositoryRoot, string projectRelativePath, string query, string configuration)
    {
        string projectFile = Path.GetFullPath(Path.Combine(repositoryRoot, projectRelativePath));
        if (!File.Exists(projectFile))
        {
            throw new InvalidOperationException($"MSBuild version project does not exist: {projectRelativePath}");
        }

        PackageTestProcessResult result = PackageTestProcess.Run("dotnet",
            ["msbuild", projectFile, "-nologo", $"-p:Configuration={configuration}", query], repositoryRoot, 30_000);
        if (result.ExitCode != 0)
        {
            string diagnostic = (result.StandardOutput + result.StandardError).Replace('\r', ' ').Replace('\n', ' ');
            throw new InvalidOperationException($"MSBuild version query {query} failed for {projectRelativePath} with exit {result.ExitCode}: {diagnostic[..Math.Min(diagnostic.Length, 500)]}");
        }

        return result.StandardOutput;
    }

    private static string ValidateVersion(string value, string identity)
    {
        if (!Regex.IsMatch(value, @"\A(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*)){1,3}(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"MSBuild version {identity} did not resolve to a concrete NuGet version.");
        }

        return value;
    }
}
