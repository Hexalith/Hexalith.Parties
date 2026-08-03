namespace Hexalith.Parties.AppHost;

/// <summary>Resolves topology projects without adding compile-time dependency project references.</summary>
internal static class ReferenceProjectResolver
{
    /// <summary>Returns the first existing project from the supported standalone and umbrella layouts.</summary>
    public static string? Find(DirectoryInfo repositoryRoot, string repositoryName, string projectRelativePath) =>
        CandidateProjectPaths(repositoryRoot, repositoryName, projectRelativePath).FirstOrDefault(File.Exists);

    /// <summary>Resolves a required topology project or reports every probed path and the correct workspace command.</summary>
    public static string ResolveRequired(
        DirectoryInfo repositoryRoot,
        string repositoryName,
        string projectRelativePath)
    {
        string[] candidates = CandidateProjectPaths(repositoryRoot, repositoryName, projectRelativePath);
        string? projectPath = candidates.FirstOrDefault(File.Exists);
        if (projectPath is not null)
        {
            return projectPath;
        }

        throw new FileNotFoundException(
            $"Required topology project '{projectRelativePath}' from {repositoryName} was not found. "
            + $"Probed: {string.Join(", ", candidates.Select(static path => $"'{path}'"))}. "
            + InitializationGuidance(repositoryRoot, repositoryName),
            projectRelativePath);
    }

    /// <summary>Builds workspace-aware root-only submodule initialization guidance.</summary>
    public static string InitializationGuidance(DirectoryInfo repositoryRoot, string repositoryName)
    {
        DirectoryInfo commandRoot = IsUmbrellaSubmodule(repositoryRoot)
            ? repositoryRoot.Parent!.Parent!
            : repositoryRoot;
        return $"Run 'git -C \"{commandRoot.FullName}\" submodule update --init references/{repositoryName}' "
            + "from the root that declares the dependency. Do not use recursive submodule initialization.";
    }

    private static string[] CandidateProjectPaths(
        DirectoryInfo repositoryRoot,
        string repositoryName,
        string projectRelativePath)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRelativePath);

        string parent = repositoryRoot.Parent?.FullName ?? repositoryRoot.FullName;
        string[] dependencyRoots = IsUmbrellaSubmodule(repositoryRoot)
            ?
            [
                Path.Combine(parent, repositoryName),
                Path.Combine(repositoryRoot.FullName, "references", repositoryName),
            ]
            :
            [
                Path.Combine(repositoryRoot.FullName, "references", repositoryName),
                Path.Combine(parent, repositoryName),
                Path.Combine(parent, "references", repositoryName),
            ];

        return dependencyRoots
            .Select(root => Path.GetFullPath(Path.Combine(root, projectRelativePath)))
            .ToArray();
    }

    private static bool IsUmbrellaSubmodule(DirectoryInfo repositoryRoot) =>
        repositoryRoot.Parent is not null
        && string.Equals(repositoryRoot.Parent.Name, "references", StringComparison.OrdinalIgnoreCase)
        && repositoryRoot.Parent.Parent is not null;
}
