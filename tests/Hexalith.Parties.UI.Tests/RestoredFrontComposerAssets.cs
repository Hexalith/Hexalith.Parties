using System.Reflection;

namespace Hexalith.Parties.UI.Tests;

/// <summary>Finds the restored shell asset across NuGet's primary and fallback package folders.</summary>
internal static class RestoredFrontComposerAssets
{
    /// <summary>Resolves the catalog-selected stylesheet from the assembly's evaluated restore metadata.</summary>
    /// <param name="assembly">The assembly carrying the package version and restored folder metadata.</param>
    /// <returns>The existing packaged stylesheet path.</returns>
    internal static string ResolveShellStylesheet(Assembly assembly)
    {
        string folders = ReadBuildMetadata(assembly, "NuGetPackageFolders");
        string version = ReadBuildMetadata(assembly, "HexalithFrontComposerVersion");
        foreach (string folder in folders.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string asset = Path.Combine(folder, "hexalith.frontcomposer.shell", version, "staticwebassets", "css", "fc-shell.css");
            if (File.Exists(asset))
            {
                return asset;
            }
        }

        throw new InvalidOperationException($"Restore Hexalith.FrontComposer.Shell {version}; its stylesheet is missing from the evaluated NuGet package folders.");
    }

    private static string ReadBuildMetadata(Assembly assembly, string name)
    {
        string? value = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == name)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Restore and rebuild UI.Tests to provide evaluated {name} metadata.");
        }

        return value;
    }
}
