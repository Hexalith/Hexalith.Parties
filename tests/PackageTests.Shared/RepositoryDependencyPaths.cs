using System.Reflection;

namespace Hexalith.Parties.PackageTests;

/// <summary>Uses the pinned tool's exact dependency roots for source-mode repository assertions.</summary>
internal static class RepositoryDependencyPaths
{
    /// <summary>Resolves a repository-relative path, preserving temporary fixture repositories.</summary>
    internal static string Resolve(string repositoryRoot, string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/');
        string[] segments = normalized.Split('/', 3);
        string? selected = SelectedRoot(repositoryRoot, segments);
        return selected is null
            ? Path.GetFullPath(Path.Combine(repositoryRoot, relativePath))
            : Path.GetFullPath(Path.Combine(selected, segments.Length == 3 ? segments[2] : string.Empty));
    }

    /// <summary>Distinguishes an explicit source override from immutable dependency consumption.</summary>
    internal static bool IsMappedDependency(string repositoryRoot, string relativePath)
        => SelectedRoot(repositoryRoot, relativePath.Replace('\\', '/').Split('/', 3)) is not null;

    private static string? SelectedRoot(string repositoryRoot, string[] segments)
        => segments.Length >= 2 && segments[0] == "references" && IsOwningRepository(repositoryRoot)
            ? typeof(RepositoryDependencyPaths).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .SingleOrDefault(item => item.Key == "HexalithTestRoot:" + segments[1])?.Value is { Length: > 0 } selected
                    ? selected : null
            : null;

    private static bool IsOwningRepository(string repositoryRoot)
    {
        string? declaredRoot = typeof(RepositoryDependencyPaths).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(item => item.Key == "HexalithTestRepositoryRoot")?.Value;
        if (!string.IsNullOrWhiteSpace(declaredRoot))
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(declaredRoot)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Parties.slnx")))
            {
                return Path.GetFullPath(repositoryRoot) == current.FullName;
            }
        }
        return false;
    }
}
