using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed partial class AccessibilityStyleGuardTests
{
    private static readonly string[] AppOwnedRoots =
    [
        "src/Hexalith.Parties.UI/Components",
    ];

    private static readonly string[] ForbiddenColorLiterals =
    [
        "#",
        "rgb(",
        "rgba(",
        "hsl(",
        "hsla(",
    ];

    [Fact]
    public void App_owned_styles_do_not_suppress_focus_without_focus_visible_restore()
    {
        foreach ((string RelativePath, string Content) file in ReadAppOwnedStyles())
        {
            MatchCollection suppressions = FocusSuppressionRegex().Matches(file.Content);
            foreach (Match suppression in suppressions)
            {
                int restoreStart = Math.Max(0, suppression.Index - 500);
                int restoreLength = Math.Min(file.Content.Length - restoreStart, 1_000);
                string nearby = file.Content.Substring(restoreStart, restoreLength);

                nearby.ShouldContain(
                    ":focus-visible",
                    Case.Insensitive,
                    $"{file.RelativePath} suppresses focus styling without a nearby :focus-visible restore.");
            }
        }
    }

    [Fact]
    public void App_owned_interactive_styles_do_not_use_raw_color_literals_or_raw_teal()
    {
        foreach ((string RelativePath, string Content) file in ReadAppOwnedStyles())
        {
            foreach (string forbidden in ForbiddenColorLiterals)
            {
                file.Content.ShouldNotContain(forbidden, Case.Insensitive, $"Forbidden color literal '{forbidden}' found in {file.RelativePath}.");
            }

            file.Content.ShouldNotContain("#0097A7", Case.Insensitive, $"Raw brand teal found in {file.RelativePath}.");
        }
    }

    [Fact]
    public void MainLayout_accessibility_css_declares_forced_colors_and_reduced_motion_rules()
    {
        string content = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css"));

        content.ShouldContain("@media (forced-colors: active)", Case.Insensitive);
        content.ShouldContain("@media (prefers-reduced-motion: reduce)", Case.Insensitive);
        content.ShouldContain("--colorStrokeFocus2", Case.Insensitive);
    }

    private static IEnumerable<(string RelativePath, string Content)> ReadAppOwnedStyles()
    {
        string repositoryRoot = FindRepositoryRoot();

        foreach (string root in AppOwnedRoots)
        {
            string absoluteRoot = Path.Combine(repositoryRoot, root);
            foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*.css", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(repositoryRoot, file);
                if (relativePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return (relativePath, File.ReadAllText(file));
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Parties.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    [GeneratedRegex(@"(?:outline|box-shadow)\s*:\s*none", RegexOptions.IgnoreCase)]
    private static partial Regex FocusSuppressionRegex();
}
