namespace Hexalith.Parties.PackageTests;

internal sealed record PackageTestProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    internal string Diagnostic => string.IsNullOrWhiteSpace(StandardError) ? StandardOutput : StandardError;
}
