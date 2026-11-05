namespace Hexalith.Parties.Platform.IntegrationTests;

using System.Text.Json;

using Shouldly;

/// <summary>Uses the actual published mapping and explicitly local metadata controls, never a simulated CI owner.</summary>
public sealed class NativeDiagnosticBindingTests
{
    /// <summary>Checks the surviving caller route and immutable cross-document refusals with GitHub variables absent.</summary>
    /// <param name="mutation">The local metadata boundary to exercise.</param>
    [Theory]
    [InlineData("valid-local")]
    [InlineData("current-valid-local")]
    [InlineData("foreign-run")]
    [InlineData("foreign-attempt")]
    [InlineData("changed-mapping")]
    [InlineData("changed-context")]
    [InlineData("foreign-model-output")]
    [InlineData("lifecycle-local")]
    [InlineData("lifecycle-foreign-receipt")]
    [InlineData("lifecycle-foreign-source")]
    public void PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(string mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS").ShouldBeNull();
        Environment.GetEnvironmentVariable("GITHUB_WORKSPACE").ShouldBeNull();
        string prepared = Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("CustomAfterMicrosoftCommonProps"))!, "mapping.json");
        using JsonDocument mapping = JsonDocument.Parse(File.ReadAllText(prepared));
        string caller = mapping.RootElement.GetProperty("Root").GetString()!;
        string hash = mapping.RootElement.GetProperty("ContentHash").GetString()!;
        string control = Directory.CreateTempSubdirectory("parties-local-handoff-control-").FullName;
        string workspace = Directory.CreateDirectory(Path.Combine(control, "workspace")).FullName;
        string state = Directory.CreateDirectory(Path.Combine(control, "state")).FullName;
        string run = Guid.NewGuid().ToString("N"), attempt = Guid.NewGuid().ToString("N"), environment = Guid.NewGuid().ToString("N");
        string? retained = null;
        try
        {
            Directory.CreateDirectory(Path.Combine(workspace, "source-mapping"));
            Directory.CreateDirectory(Path.Combine(workspace, "native-tests"));
            File.Copy(prepared, Path.Combine(workspace, "source-mapping", "mapping.json"));
            File.WriteAllText(Path.Combine(workspace, "test-environment.json"), JsonSerializer.Serialize(new
            {
                bindings = new
                {
                    run = new { runId = run, ownerRunId = run, attemptId = attempt, environmentId = environment, role = "owner" },
                    environment = new { executionContext = new { kind = "local" } },
                },
            }));
            File.WriteAllText(Path.Combine(workspace, "plan.json"), JsonSerializer.Serialize(new
            {
                schema = "hexalith.g4-run-plan.v1", runId = run, workspace, resourceClaimStateDirectory = state,
                attemptId = mutation == "foreign-attempt" ? Guid.NewGuid().ToString("N") : attempt, environmentId = environment,
                sourceMappingHash = hash, sourceMapping = new { root = caller, contentHash = hash },
                selectedTest = new { name = mutation == "current-valid-local" ? "platform-current-package" : mutation.StartsWith("lifecycle-", StringComparison.Ordinal) ? "lifecycle-package" : "platform-package" },
                platformModelAssemblyPath = Path.Combine(workspace, "hosts", mutation == "foreign-model-output" ? "unbound-output" : "platform-model", "Hexalith.Platform.Composition.dll"),
            }));
            File.WriteAllText(Path.Combine(state, run + ".json"), JsonSerializer.Serialize(new
            {
                schema = "hexalith.g4-run-state.v1", runId = run, workspace, attemptId = attempt, environmentId = environment,
                executionContext = new { kind = mutation == "changed-context" ? "unsupported-local-control" : "local" },
            }));
            Dictionary<string, string> handoff = new(StringComparer.Ordinal)
            {
                ["HEXALITH_G4_RUN_ID"] = mutation == "foreign-run" ? Guid.NewGuid().ToString("N") : run,
                ["HEXALITH_TEST_ENVIRONMENT_PATH"] = Path.Combine(workspace, "test-environment.json"),
                ["HEXALITH_G4_PLAN_PATH"] = Path.Combine(workspace, "plan.json"),
                ["HEXALITH_G4_TEST_REPORT_PATH"] = Path.Combine(workspace, "native-tests", "native.trx"),
                ["HEXALITH_G4_SOURCE_MAPPING_PATH"] = Path.Combine(workspace, "source-mapping", "mapping.json"),
                ["HEXALITH_SOURCE_MAPPING_HASH"] = mutation == "changed-mapping" ? new string('0', 64) : hash,
            };
            if (mutation is not ("valid-local" or "current-valid-local") && !mutation.StartsWith("lifecycle-", StringComparison.Ordinal))
            {
                Should.Throw<InvalidDataException>(() => NativeDiagnosticBinding.Read(run, handoff));
                return;
            }
            NativeDiagnosticBinding binding = NativeDiagnosticBinding.Read(run, handoff);
            binding.CallerRoot.ShouldBe(caller);
            binding.OwnerWorkspace.ShouldBe(workspace);
            binding.ExecutionContext.GetProperty("kind").GetString().ShouldBe("local");
            if (mutation.StartsWith("lifecycle-", StringComparison.Ordinal))
            {
                binding.ArtifactDirectory.ShouldBe(Path.Combine(caller, "TestResults", "ci-lifecycle"));
                string role = Directory.CreateDirectory(Path.Combine(control, "hexalith-ci-qualification", "passing")).FullName;
                string receipt = Path.Combine(role, "receipt.json");
                File.WriteAllText(receipt, JsonSerializer.Serialize(new
                {
                    schema = "hexalith.module.ci-owner-receipt.v1", runId = run,
                    attemptId = mutation == "lifecycle-foreign-receipt" ? Guid.NewGuid().ToString("N") : attempt,
                    environmentId = environment, stateDirectory = state, executionContext = new { kind = "local" },
                }));
                string journal = Path.Combine(control, "observations.json");
                File.WriteAllText(journal, JsonSerializer.Serialize(new
                {
                    schema = "hexalith.ci-lifecycle-qualification.v1", classification = "baseline-only",
                    source = mutation == "lifecycle-foreign-source" ? new string('0', 40) : binding.Revision,
                    mappingHash = hash, executionContext = new { kind = "local" },
                    owners = new[] { new { label = "passing", receipt } },
                }));
                if (mutation != "lifecycle-local")
                {
                    Should.Throw<InvalidDataException>(() => binding.VerifyLifecycleArtifact(journal));
                    return;
                }
                binding.VerifyLifecycleArtifact(journal);
            }
            else binding.ArtifactDirectory.ShouldBe(Path.Combine(caller, "TestResults", "module-integration"));
            Directory.CreateDirectory(binding.ArtifactDirectory);
            NativeAssemblyObservations.WritePrivateObservation(binding.ArtifactDirectory, "local-control-" + run, "{\"scope\":\"local-handoff-io-only\"}");
            retained = Path.Combine(binding.ArtifactDirectory, "loaded-assemblies-local-control-" + run + ".json");
            byte[] original = File.ReadAllBytes(retained);
            Directory.Delete(workspace, recursive: true);
            File.ReadAllBytes(retained).ShouldBe(original);
        }
        finally
        {
            if (retained is not null) File.Delete(retained);
            Directory.Delete(control, recursive: true);
        }
    }
    /// <summary>Ordinary compatible batches retain containment; current purpose requires one exclusive profile.</summary>
    [Theory]
    [InlineData("platform-package", "platform-package,lifecycle-package", true)]
    [InlineData("platform-package", "lifecycle-package", false)]
    [InlineData("platform-current-package", "platform-current-package", true)]
    [InlineData("platform-current-package", "platform-current-package,platform-package", false)]
    [InlineData("platform-package", "platform-package,platform-current-package", false)]
    [InlineData("platform-current-package", "platform-current-package,platform-current-package", false)]
    public void InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(string profile, string profiles, bool accepted)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        string directory = Directory.CreateTempSubdirectory("parties-local-invocation-").FullName;
        try
        {
            using JsonDocument context = JsonDocument.Parse("{\"kind\":\"local\"}");
            var binding = new NativeDiagnosticBinding(new string('1', 32), new string('2', 32), new string('3', 32), directory,
                directory, new string('a', 40), new string('b', 64), context.RootElement.Clone(), directory, "local-model", profile);
            Directory.CreateDirectory(binding.ArtifactDirectory);
            File.WriteAllBytes(Path.Combine(binding.ArtifactDirectory, "invocation.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                classification = "baseline-only", executionContext = context.RootElement, profiles = profiles.Split(','),
            }));
            if (accepted) binding.VerifyCallerArtifact();
            else Should.Throw<InvalidDataException>(binding.VerifyCallerArtifact);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
