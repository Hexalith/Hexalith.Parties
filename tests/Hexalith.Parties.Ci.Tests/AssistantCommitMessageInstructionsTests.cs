namespace Hexalith.Parties.Ci.Tests;

public sealed class AssistantCommitMessageInstructionsTests
{
    private const string CanonicalEntryPoint = "AGENTS.md";

    [Fact]
    public void AssistantEntryPointsAreByteIdentical()
    {
        byte[] expected = File.ReadAllBytes(CiTestPaths.RepoFile(CanonicalEntryPoint));
        string[] entryPoints =
        [
            "CLAUDE.md",
            ".github/copilot-instructions.md",
        ];

        foreach (string entryPoint in entryPoints)
        {
            byte[] actual = File.ReadAllBytes(CiTestPaths.RepoFile(entryPoint));
            actual.SequenceEqual(expected).ShouldBeTrue($"{entryPoint} must be byte-identical to {CanonicalEntryPoint}.");
        }
    }

    [Fact]
    public void SupportedAssistantSurfacesReceiveTheCompleteGenerationContract()
    {
        string instructions = ReadInstructions();

        instructions.ShouldContain("Claude, Codex, Cursor, GitHub");
        instructions.ShouldContain("Copilot, or Visual Studio");
        instructions.ShouldContain("active repository's commitlint configuration and Git instructions");
        instructions.ShouldContain("`<type>[optional scope][!]: <description>`");
        instructions.ShouldContain("`feat`, `fix`, `perf`, `docs`, `refactor`, `test`, `revert`, `build`");
        instructions.ShouldContain("`ci`, or `style`");
        instructions.ShouldContain("by release impact and never use `chore`");
        instructions.ShouldContain("Honor configured header and body limits");
        instructions.ShouldContain("prefer a subject near 50 characters");
        instructions.ShouldContain("body lines near 72 characters");
        instructions.ShouldContain("`!` or a `BREAKING CHANGE:` footer");
    }

    [Fact]
    public void DocumentationOnlyGenerationRequiresAValidatedTypedMessage()
    {
        string instructions = ReadInstructions();

        instructions.ShouldContain("documentation-only");
        instructions.ShouldContain("`docs: align commit guidance`");
        instructions.ShouldContain("Validate the exact candidate, including any body and footers");
        instructions.ShouldContain("repository-pinned commitlint");
        instructions.ShouldContain("Revise it until both");
        instructions.ShouldContain("commitlint and the stricter Hexalith policy pass");
    }

    [Fact]
    public void InvalidConventionalShapeMustBeReplaced()
    {
        string instructions = ReadInstructions();

        instructions.ShouldContain("plain-English or default-shaped subjects");
        instructions.ShouldContain("`Update ...`");
        instructions.ShouldContain("`Add ...`");
        instructions.ShouldContain("`Fix ...`");
        instructions.ShouldContain("`Bump ...`");
        instructions.ShouldContain("Start the description lowercase");
        instructions.ShouldContain("imperative mood");
        instructions.ShouldContain("omit a");
        instructions.ShouldContain("trailing period");
    }

    [Fact]
    public void CommitlintIgnoredDefaultsMustStillBeRejected()
    {
        string instructions = ReadInstructions();

        instructions.ShouldContain("Reject merge, fixup, squash");
        instructions.ShouldContain("version-only, and Git-generated revert subjects");
        instructions.ShouldContain("even when commitlint ignores");
        instructions.ShouldContain("Intentional reverts use `revert: ...`");
    }

    [Fact]
    public void UnavailableToolingMustBeReportedWithoutClaimingValidation()
    {
        string instructions = ReadInstructions();

        instructions.ShouldContain("If tooling is unavailable");
        instructions.ShouldContain("commitlint validation was not run");
        instructions.ShouldContain("never claim");
        instructions.ShouldContain("tool-verified compliance");
    }

    [Fact]
    public void PersistentProjectContextUsesTheCommitlintAwareHexalithPolicy()
    {
        string context = CiTestPaths.ReadRepoFile("_bmad-output/project-context.md");

        context.ShouldContain("active commitlint plus stricter Hexalith policy");
        context.ShouldContain("`<type>[optional scope][!]: <description>`");
        context.ShouldContain("**never use `chore`**");
        context.ShouldContain("Descriptions start lowercase");
        context.ShouldContain("imperative mood");
        context.ShouldContain("trailing period");
        context.ShouldContain("prefer subjects near 50 characters and body lines near 72");
        context.ShouldContain("`BREAKING CHANGE:` footer");
        context.ShouldContain("Reject plain-English");
        context.ShouldContain("merge, fixup, squash, version-only, or Git-generated revert defaults");
        context.ShouldContain("repository-pinned commitlint");
        context.ShouldContain("validation was not run");
        context.ShouldNotContain("`feat:`, `fix:`, `chore:`");
    }

    private static string ReadInstructions() => CiTestPaths.ReadRepoFile(CanonicalEntryPoint);
}
