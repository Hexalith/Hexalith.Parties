namespace Hexalith.Parties.Platform.IntegrationTests;

using System.Text.Json;

using Shouldly;

/// <summary>Uses the actual published mapping and explicitly local metadata controls, never a simulated CI owner.</summary>
[Collection(CiLifecycleHoldTests.ProcessHandoffCollection)]
public sealed class NativeDiagnosticBindingTests
{
    /// <summary>Checks the surviving caller route and immutable cross-document refusals with GitHub variables absent.</summary>
    /// <param name="mutation">The local metadata boundary to exercise.</param>
    [Theory]
    [InlineData("valid-local")]
    [InlineData("foreign-run")]
    [InlineData("foreign-attempt")]
    [InlineData("changed-mapping")]
    [InlineData("changed-context")]
    [InlineData("foreign-model-output")]
    [InlineData("lifecycle-local")]
    [InlineData("lifecycle-foreign-receipt")]
    [InlineData("lifecycle-foreign-source")]
    [InlineData("held-platform-package")]
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
        bool lifecycle = mutation.StartsWith("lifecycle-", StringComparison.Ordinal), held = mutation == "held-platform-package";
        string? previousRun = Environment.GetEnvironmentVariable("HEXALITH_G4_RUN_ID");
        string? previousHold = Environment.GetEnvironmentVariable(CiLifecycleHold.VariableName);
        string hold = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hexalith-builds", "ci-qualification", run);
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
                selectedTest = new { name = lifecycle ? "lifecycle-package" : "platform-package" },
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
            if (mutation != "valid-local" && !lifecycle && !held)
            {
                Should.Throw<InvalidDataException>(() => NativeDiagnosticBinding.Read(run, handoff));
                return;
            }
            if (held)
            {
                // Builds 6.3.x forwards no hold variable: only the controller's wait control for this run signals the hold.
                Environment.SetEnvironmentVariable(CiLifecycleHold.VariableName, null);
                Environment.SetEnvironmentVariable("HEXALITH_G4_RUN_ID", run);
                Directory.CreateDirectory(hold);
                File.WriteAllText(Path.Combine(hold, "control.json"), JsonSerializer.Serialize(new { runId = run, action = "wait" }));
            }
            NativeDiagnosticBinding binding = NativeDiagnosticBinding.Read(run, handoff);
            if (held)
            {
                Environment.SetEnvironmentVariable("HEXALITH_G4_RUN_ID", previousRun);
                Environment.SetEnvironmentVariable(CiLifecycleHold.VariableName, previousHold);
                binding.LifecycleInvocation.ShouldBeTrue();
            }
            binding.CallerRoot.ShouldBe(caller);
            binding.OwnerWorkspace.ShouldBe(workspace);
            JsonElement.DeepEquals(binding.PlatformIdentity, mapping.RootElement.GetProperty("PlatformIdentity")).ShouldBeTrue();
            binding.ExecutionContext.GetProperty("kind").GetString().ShouldBe("local");
            if (lifecycle || held)
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
                if (mutation is not ("lifecycle-local" or "held-platform-package"))
                {
                    Should.Throw<InvalidDataException>(() => binding.VerifyLifecycleArtifact(journal));
                    return;
                }
                binding.VerifyLifecycleArtifact(journal);
            }
            // An outer controller hold of this real native run reroutes the ordinary entry as well.
            else binding.ArtifactDirectory.ShouldBe(Path.Combine(caller, "TestResults", binding.LifecycleInvocation ? "ci-lifecycle" : "module-integration"));
            Directory.CreateDirectory(binding.ArtifactDirectory);
            NativeAssemblyObservations.WritePrivateObservation(binding.ArtifactDirectory, "local-control-" + run, "{\"scope\":\"local-handoff-io-only\"}");
            retained = Path.Combine(binding.ArtifactDirectory, "loaded-assemblies-local-control-" + run + ".json");
            byte[] original = File.ReadAllBytes(retained);
            Directory.Delete(workspace, recursive: true);
            File.ReadAllBytes(retained).ShouldBe(original);
        }
        finally
        {
            if (held)
            {
                Environment.SetEnvironmentVariable("HEXALITH_G4_RUN_ID", previousRun);
                Environment.SetEnvironmentVariable(CiLifecycleHold.VariableName, previousHold);
            }
            if (Directory.Exists(hold)) Directory.Delete(hold, recursive: true);
            if (retained is not null) File.Delete(retained);
            Directory.Delete(control, recursive: true);
        }
    }

    /// <summary>A held integration entry uses its lifecycle journal and never requires the ordinary caller invocation.</summary>
    /// <param name="held">Whether the owner invocation runs under the controller hold.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HeldIntegrationInvocationRoutesToTheLifecycleJournal(bool held)
    {
        string directory = Directory.CreateTempSubdirectory("parties-local-held-invocation-").FullName;
        try
        {
            using JsonDocument context = JsonDocument.Parse("{\"kind\":\"local\"}");
            var binding = new NativeDiagnosticBinding(new string('1', 32), new string('2', 32), new string('3', 32), directory,
                directory, new string('a', 40), new string('b', 64), context.RootElement.Clone(), directory, "local-model", "platform-package")
            {
                LifecycleInvocation = held,
            };
            binding.ArtifactDirectory.ShouldBe(Path.Combine(directory, "TestResults", held ? "ci-lifecycle" : "module-integration"));
            string journal = Directory.CreateDirectory(Path.Combine(directory, "TestResults", "ci-lifecycle")).FullName;
            string receipt = Path.Combine(Directory.CreateDirectory(Path.Combine(directory, "hexalith-ci-qualification", "passing")).FullName, "receipt.json");
            File.WriteAllBytes(receipt, JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = "hexalith.module.ci-owner-receipt.v1", runId = binding.RunId, attemptId = binding.AttemptId,
                environmentId = binding.EnvironmentId, stateDirectory = directory, executionContext = context.RootElement,
            }));
            File.WriteAllBytes(Path.Combine(journal, "observations.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = "hexalith.ci-lifecycle-qualification.v1", classification = "baseline-only", source = binding.Revision,
                mappingHash = binding.MappingHash, executionContext = context.RootElement, owners = new[] { new { label = "passing", receipt } },
            }));

            // No invocation.json exists: only the ordinary, unheld entry requires it.
            if (held) binding.VerifyCallerArtifact();
            else Should.Throw<InvalidDataException>(binding.VerifyCallerArtifact);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Compatible declared package batches admit the integration entry once.</summary>
    [Theory]
    [InlineData("platform-package", "platform-package,lifecycle-package", true)]
    [InlineData("platform-package", "lifecycle-package", false)]
    [InlineData("platform-package", "platform-package", true)]
    [InlineData("platform-package", "unknown-package,platform-package", false)]
    [InlineData("platform-package", "platform-package,platform-source", false)]
    [InlineData("platform-package", "platform-package,platform-package", false)]
    public void InvocationRequiresTheDeclaredIntegrationProfileOnce(string profile, string profiles, bool accepted)
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
