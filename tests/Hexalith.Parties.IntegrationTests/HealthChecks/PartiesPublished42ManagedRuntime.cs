using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Bounds the measured published SDK 4.2 native test workaround without changing product runtimes.</summary>
internal static class PartiesPublished42ManagedRuntime
{
    private const string Refusal = "The published SDK 4.2 native test profile requires trusted shared Core/JSON 11.0.0 and highest installed Core/ASP.NET Core 11.0.0; other runtimes require qualification.";

    /// <summary>Gets whether this test assembly was built for the exact published SDK 4.2 profile.</summary>
    internal static bool IsSelected => typeof(PartiesPublished42ManagedRuntime).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "PartiesPublished42ManagedRuntime").Value == "true";

    /// <summary>Validates the actual parent and finite installed runtimes before owned native effects.</summary>
    internal static async Task RequireAsync(CancellationToken cancellationToken)
    {
        if (!IsSelected) { return; }
        ValidateHost(Environment.Version, typeof(object).Assembly.Location,
            typeof(JsonElement).Assembly.GetName().Version, typeof(JsonElement).Assembly.Location);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("--list-runtimes");
        using var process = Process.Start(start) ?? throw new InvalidOperationException(Refusal);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        Task<string> output = process.StandardOutput.ReadToEndAsync(budget.Token);
        Task<string> error = process.StandardError.ReadToEndAsync(budget.Token);
        try
        {
            await process.WaitForExitAsync(budget.Token).ConfigureAwait(false);
            string inventory = await output.ConfigureAwait(false);
            _ = await error.ConfigureAwait(false);
            if (process.ExitCode != 0 || inventory.Length > 65536) { throw new InvalidOperationException(Refusal); }
            ValidateInventory(inventory);
        }
        catch
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            throw;
        }
    }

    /// <summary>Refuses package-local JSON, unqualified parent versions and foreign shared-runtime paths.</summary>
    internal static void ValidateHost(Version runtime, string coreLibrary, Version? jsonVersion, string jsonLibrary)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        DirectoryInfo directory = new(Path.GetDirectoryName(Path.GetFullPath(coreLibrary)) ?? throw new InvalidOperationException(Refusal));
        if (runtime != new Version(11, 0, 0) || jsonVersion != new Version(11, 0, 0, 0)
            || directory.Name != "11.0.0" || directory.Parent?.Name != "Microsoft.NETCore.App" || directory.Parent.Parent?.Name != "shared"
            || !Path.GetFileName(coreLibrary).Equals("System.Private.CoreLib.dll", comparison)
            || !Path.GetFullPath(jsonLibrary).Equals(Path.Combine(directory.FullName, "System.Text.Json.dll"), comparison))
        {
            throw new InvalidOperationException(Refusal);
        }
    }

    /// <summary>Keeps resource-local LatestMajor bounded to the measured finite shared runtime pair.</summary>
    internal static void ValidateInventory(string inventory)
    {
        foreach (string family in new[] { "Microsoft.NETCore.App", "Microsoft.AspNetCore.App" })
        {
            List<Version> versions = [];
            foreach (string line in inventory.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields[0] != family) { continue; }
                if (fields.Length < 3 || !fields[2].StartsWith('[') || !line.EndsWith(']')
                    || !Version.TryParse(fields[1], out Version? version))
                {
                    throw new InvalidOperationException(Refusal);
                }
                versions.Add(version);
            }
            if (versions.Count == 0 || versions.Max() != new Version(11, 0, 0)) { throw new InvalidOperationException(Refusal); }
        }
    }
}
