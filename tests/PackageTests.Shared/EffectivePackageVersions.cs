using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.Parties.PackageTests;

/// <summary>Reads live dependency versions using the repository's actual MSBuild import and condition order.</summary>
internal static class EffectivePackageVersions
{
    private const string DefaultProject = "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj";

    /// <summary>Evaluates one concrete version property in the selected package configuration.</summary>
    internal static string ReadProperty(string repositoryRoot, string propertyName, string projectRelativePath = DefaultProject, string configuration = "Release", IReadOnlyDictionary<string, string>? globalProperties = null)
    {
        if (!Regex.IsMatch(propertyName, @"\A[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("An MSBuild property name is required.", nameof(propertyName));
        }

        string value = Query(repositoryRoot, projectRelativePath, $"-getProperty:{propertyName}", configuration, globalProperties: globalProperties);
        // MSBuild appends one line ending; any additional CR/LF belongs to the value and is invalid.
        value = value.EndsWith("\r\n", StringComparison.Ordinal) ? value[..^2]
            : value.EndsWith('\n') ? value[..^1] : value;
        return ValidateVersion(value, propertyName);
    }

    /// <summary>Evaluates central package items after all applicable root pins, imports, and assignments.</summary>
    internal static IReadOnlyDictionary<string, string> ReadPackages(string repositoryRoot, string projectRelativePath = DefaultProject, string configuration = "Release", bool isolateEnvironment = false, IReadOnlyDictionary<string, string>? globalProperties = null)
    {
        string output = Query(repositoryRoot, projectRelativePath, "-getItem:PackageVersion", configuration, isolateEnvironment, globalProperties);
        using JsonDocument document = JsonDocument.Parse(output);
        return document.RootElement.GetProperty("Items").GetProperty("PackageVersion").EnumerateArray()
            .ToDictionary(
                static item => item.GetProperty("Identity").GetString()!,
                static item => ValidateVersion(item.GetProperty("Version").GetString() ?? string.Empty, item.GetProperty("Identity").GetString()!),
                StringComparer.Ordinal);
    }

    private static string Query(string repositoryRoot, string projectRelativePath, string query, string configuration, bool isolateEnvironment = false, IReadOnlyDictionary<string, string>? globalProperties = null)
    {
        string projectFile = Path.GetFullPath(Path.Combine(repositoryRoot, projectRelativePath));
        if (!File.Exists(projectFile))
        {
            throw new InvalidOperationException($"MSBuild version project does not exist: {projectRelativePath}");
        }

        var startInfo = new ProcessStartInfo("dotnet");
        foreach (string argument in new[] { "msbuild", projectFile, "-nologo", $"-p:Configuration={configuration}", query })
        {
            startInfo.ArgumentList.Add(argument);
        }
        if (globalProperties is not null)
        {
            foreach ((string name, string value) in globalProperties)
            {
                if (!Regex.IsMatch(name, @"\A[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant))
                {
                    throw new ArgumentException("An MSBuild property name is required.", nameof(globalProperties));
                }

                // A property is one process argument; escape MSBuild's own property-list
                // separators as well, including when they occur in a catalog path.
                string escaped = value.Replace("%", "%25", StringComparison.Ordinal)
                    .Replace(";", "%3B", StringComparison.Ordinal)
                    .Replace(",", "%2C", StringComparison.Ordinal);
                startInfo.ArgumentList.Add($"-p:{name}={escaped}");
            }
        }
        if (isolateEnvironment)
        {
            // Historical versions come only from the committed inputs. Keep the process-launch
            // environment, not arbitrary environment properties imported by MSBuild.
            HashSet<string> platform = new(StringComparer.OrdinalIgnoreCase)
            {
                "PATH", "PATHEXT", "HOME", "USERPROFILE", "TMP", "TEMP", "TMPDIR", "SystemRoot",
                "SystemDrive", "WINDIR", "COMSPEC", "APPDATA", "LOCALAPPDATA", "ProgramFiles",
                "ProgramFiles(x86)", "ProgramW6432", "DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86",
                "LD_LIBRARY_PATH", "DYLD_LIBRARY_PATH", "LANG", "LC_ALL", "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",
            };
            foreach (string name in startInfo.Environment.Keys.Where(name => !platform.Contains(name)).ToArray())
            {
                startInfo.Environment.Remove(name);
            }
        }
        PackageTestProcessResult result = PackageTestProcess.Run(startInfo, repositoryRoot, 30_000);
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
            string diagnostic = value.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
            throw new InvalidOperationException($"MSBuild version {identity} did not resolve to a concrete NuGet version: '{diagnostic}'.");
        }

        return value;
    }
}
