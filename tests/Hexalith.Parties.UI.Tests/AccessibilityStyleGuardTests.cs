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
    public void FrontComposer_shell_stylesheet_link_is_declared_in_app_component()
    {
        string root = FindRepositoryRoot();
        string appRazor = File.ReadAllText(Path.Combine(root, "src/Hexalith.Parties.UI/Components/App.razor"));
        appRazor.ShouldContain("_content/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.styles.css");
        appRazor.ShouldContain("Hexalith.Parties.UI.styles.css");

        // Skip loudly rather than pass silently. The shell stylesheet lives in a root submodule a
        // package-mode clone need not check out; a conditional that swallows the assertions would
        // report forced-colors coverage that never ran.
        string fcShellPath = Path.Combine(root, "references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/wwwroot/css/fc-shell.css");
        Assert.SkipUnless(File.Exists(fcShellPath), $"FrontComposer submodule is not checked out: {fcShellPath}");

        string content = File.ReadAllText(fcShellPath);
        content.ShouldContain("@media (forced-colors: active)", Case.Insensitive);
        content.ShouldContain("--colorStrokeFocus2", Case.Insensitive);
    }

    [Fact]
    public void App_owned_content_styles_stay_scoped_to_a_rendered_element()
    {
        string root = FindRepositoryRoot();
        string layoutDirectory = Path.Combine(root, "src/Hexalith.Parties.UI/Components/Layout");
        string mainLayoutRazor = File.ReadAllText(Path.Combine(layoutDirectory, "MainLayout.razor"));
        string mainLayoutCss = File.ReadAllText(Path.Combine(layoutDirectory, "MainLayout.razor.css"));

        // CSS isolation stamps the scope id only onto elements written in the .razor file. A layout
        // whose render tree is a single child component emits no scope attribute, so every ::deep
        // rule compiles to a selector that matches nothing and the focus indicator dies silently.
        mainLayoutRazor.ShouldContain(
            "class=\"parties-main-content\"",
            Case.Sensitive,
            "MainLayout must render an app-owned element for its scoped ::deep rules to attach to.");

        mainLayoutCss.ShouldContain(".parties-main-content ::deep");
        mainLayoutCss.ShouldContain(":focus-visible");
        mainLayoutCss.ShouldContain("--colorStrokeFocus2");
        mainLayoutCss.ShouldContain("@media (forced-colors: active)", Case.Insensitive);
        mainLayoutCss.ShouldContain("@media (prefers-reduced-motion: reduce)", Case.Insensitive);
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
