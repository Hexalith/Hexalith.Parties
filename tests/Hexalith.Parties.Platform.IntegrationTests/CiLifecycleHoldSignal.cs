namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Detects a requested persist-then-hold without the native fixtures, so the shared diagnostic binder can link it alone.</summary>
internal static class CiLifecycleHoldSignal
{
    /// <summary>The exact native handoff flag forwarded only when the owner process requested a hold.</summary>
    internal const string VariableName = "HEXALITH_CI_LIFECYCLE_HOLD";

    /// <summary>True when the owner process forwarded the hold flag, or when the controller already published wait control for this run.</summary>
    internal static bool IsRequested =>
        string.Equals(Environment.GetEnvironmentVariable(VariableName), "1", StringComparison.Ordinal)
        || ControlPath() is string path && File.Exists(path);

    /// <summary>The controller-owned control file for the issued owner run, or null without a well-formed run identity.</summary>
    /// <returns>The control file path, or null.</returns>
    internal static string? ControlPath()
    {
        string? runId = Environment.GetEnvironmentVariable("HEXALITH_G4_RUN_ID");
        if (runId is not { Length: 32 } || !runId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            return null;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "hexalith-builds",
            "ci-qualification",
            runId,
            "control.json");
    }
}
