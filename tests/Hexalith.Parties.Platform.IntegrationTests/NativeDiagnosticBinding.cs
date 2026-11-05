namespace Hexalith.Parties.Platform.IntegrationTests;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Reads only the existing native handoff and cross-bound, credential-free owner metadata.</summary>
/// <param name="RunId">The descriptor-issued owner run.</param>
/// <param name="AttemptId">The descriptor-issued owner attempt.</param>
/// <param name="EnvironmentId">The descriptor-issued environment.</param>
/// <param name="OwnerWorkspace">The disposable owner workspace.</param>
/// <param name="CallerRoot">The original Git root selected by the verified mapping.</param>
/// <param name="Revision">That root's actual Git HEAD at measurement time.</param>
/// <param name="MappingHash">The verified immutable source mapping.</param>
/// <param name="ExecutionContext">The actual context shared by descriptor and issued owner state.</param>
/// <param name="StateDirectory">The plan-bound existing owner metadata directory.</param>
/// <param name="ModelAssemblyPath">The actual prepared native composition output declared by the plan.</param>
/// <param name="Profile">The original selected declared native profile.</param>
internal sealed record NativeDiagnosticBinding(string RunId, string AttemptId, string EnvironmentId,
    string OwnerWorkspace, string CallerRoot, string Revision, string MappingHash, JsonElement ExecutionContext, string StateDirectory, string ModelAssemblyPath, string Profile)
{
    /// <summary>Gets the original caller's existing classified artifact directory, outside disposable workspaces.</summary>
    internal string ArtifactDirectory => Path.Combine(CallerRoot, "TestResults", Profile switch
    {
        "platform-package" or "platform-current-package" => "module-integration",
        "lifecycle-package" => "ci-lifecycle",
        _ => throw new InvalidDataException("Native diagnostic profile has no declared caller artifact route."),
    });

    /// <summary>Consumes only variables explicitly passed by the published native executor.</summary>
    /// <param name="runId">The fixture's validated owner run.</param>
    /// <returns>The cross-bound public diagnostic identity.</returns>
    internal static NativeDiagnosticBinding Read(string runId) => Read(runId, new[]
    {
        "HEXALITH_G4_RUN_ID", "HEXALITH_TEST_ENVIRONMENT_PATH", "HEXALITH_G4_PLAN_PATH",
        "HEXALITH_G4_TEST_REPORT_PATH", "HEXALITH_G4_SOURCE_MAPPING_PATH", "HEXALITH_SOURCE_MAPPING_HASH",
    }.ToDictionary(name => name, name => Environment.GetEnvironmentVariable(name) ?? string.Empty, StringComparer.Ordinal));

    /// <summary>Validates the exact handoff without requiring stripped GitHub environment variables.</summary>
    /// <param name="runId">The already validated fixture owner.</param>
    /// <param name="handoff">Only the published native handoff fields.</param>
    /// <returns>The actual existing ownership and caller-output binding.</returns>
    internal static NativeDiagnosticBinding Read(string runId, IReadOnlyDictionary<string, string> handoff)
    {
        if (runId.Length != 32 || runId.Any(value => value is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            || handoff["HEXALITH_G4_RUN_ID"] != runId) throw new InvalidDataException("Native diagnostic owner mismatch.");
        string workspace = Path.GetDirectoryName(Path.GetFullPath(handoff["HEXALITH_TEST_ENVIRONMENT_PATH"]))!;
        RequireEqual(handoff["HEXALITH_TEST_ENVIRONMENT_PATH"], Path.Combine(workspace, "test-environment.json"));
        RequireEqual(handoff["HEXALITH_G4_PLAN_PATH"], Path.Combine(workspace, "plan.json"));
        RequireEqual(handoff["HEXALITH_G4_TEST_REPORT_PATH"], Path.Combine(workspace, "native-tests", "native.trx"));
        RequireEqual(handoff["HEXALITH_G4_SOURCE_MAPPING_PATH"], Path.Combine(workspace, "source-mapping", "mapping.json"));
        using JsonDocument descriptor = ReadDocument(handoff["HEXALITH_TEST_ENVIRONMENT_PATH"]);
        using JsonDocument plan = ReadDocument(handoff["HEXALITH_G4_PLAN_PATH"]);
        using JsonDocument mapping = ReadDocument(handoff["HEXALITH_G4_SOURCE_MAPPING_PATH"]);
        JsonElement selected = mapping.RootElement;
        string hash = handoff["HEXALITH_SOURCE_MAPPING_HASH"];
        VerifyMapping(selected, plan.RootElement, hash);
        RequireEqual(plan.RootElement.GetProperty("schema").GetString(), "hexalith.g4-run-plan.v1");
        RequireEqual(plan.RootElement.GetProperty("runId").GetString(), runId);
        RequireEqual(plan.RootElement.GetProperty("workspace").GetString(), workspace);
        string stateDirectory = plan.RootElement.GetProperty("resourceClaimStateDirectory").GetString()!;
        using JsonDocument state = ReadDocument(Path.Combine(stateDirectory, runId + ".json"));
        JsonElement run = descriptor.RootElement.GetProperty("bindings").GetProperty("run");
        VerifyOwner(runId, workspace, run, plan.RootElement, state.RootElement);
        JsonElement context = descriptor.RootElement.GetProperty("bindings").GetProperty("environment").GetProperty("executionContext");
        if (!JsonElement.DeepEquals(context, state.RootElement.GetProperty("executionContext")))
            throw new InvalidDataException("Native diagnostic execution context mismatch.");
        VerifyContext(context);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selected.GetProperty("Root").GetString()!));
        VerifyDirectory(root);
        if (Path.GetFullPath(root).StartsWith(workspace + Path.DirectorySeparatorChar, StringComparison.Ordinal) || root == workspace)
            throw new InvalidDataException("Disposable native workspaces cannot select the caller artifact root.");
        string model = plan.RootElement.GetProperty("platformModelAssemblyPath").GetString()!;
        RequireEqual(model, Path.Combine(workspace, "hosts", "platform-model", "Hexalith.Platform.Composition.dll"));
        return new(runId, run.GetProperty("attemptId").GetString()!, run.GetProperty("environmentId").GetString()!,
            workspace, root, ReadRevision(root), hash, context.Clone(), stateDirectory, model, plan.RootElement.GetProperty("selectedTest").GetProperty("name").GetString()!);
    }

    /// <summary>Verifies the original caller helper declared this artifact directory for the exact job and profile.</summary>
    internal void VerifyCallerArtifact()
    {
        if (Profile is "platform-package" or "platform-current-package")
        {
            using JsonDocument invocation = ReadDocument(Path.Combine(ArtifactDirectory, "invocation.json"));
            RequireEqual(invocation.RootElement.GetProperty("classification").GetString(), "baseline-only");
            string?[] profiles = invocation.RootElement.GetProperty("profiles").EnumerateArray().Select(value => value.GetString()).ToArray();
            bool matches = Profile == "platform-current-package" ? profiles.SequenceEqual(new[] { Profile }, StringComparer.Ordinal)
                : profiles.Contains(Profile, StringComparer.Ordinal) && !profiles.Contains("platform-current-package", StringComparer.Ordinal);
            if (!JsonElement.DeepEquals(ExecutionContext, invocation.RootElement.GetProperty("executionContext")) || !matches)
                throw new InvalidDataException("Native diagnostics differ from the original caller artifact invocation.");
            return;
        }
        VerifyLifecycleArtifact(Path.Combine(ArtifactDirectory, "observations.json"));
    }

    /// <summary>Checks a classified lifecycle journal against this already verified owner, source and context.</summary>
    /// <param name="journalPath">The existing caller journal, or a local metadata boundary control.</param>
    internal void VerifyLifecycleArtifact(string journalPath)
    {
        using JsonDocument journal = ReadDocument(journalPath);
        JsonElement recorded = journal.RootElement;
        RequireEqual(recorded.GetProperty("schema").GetString(), "hexalith.ci-lifecycle-qualification.v1");
        RequireEqual(recorded.GetProperty("classification").GetString(), "baseline-only");
        RequireEqual(recorded.GetProperty("source").GetString(), Revision);
        RequireEqual(recorded.GetProperty("mappingHash").GetString(), MappingHash);
        if (!JsonElement.DeepEquals(ExecutionContext, recorded.GetProperty("executionContext")))
            throw new InvalidDataException("Native diagnostics differ from the existing lifecycle journal context.");
        int matches = 0;
        foreach (JsonElement owner in recorded.GetProperty("owners").EnumerateArray())
        {
            string label = owner.GetProperty("label").GetString()!;
            if (label is not ("passing" or "failing" or "peer" or "retry" or "partial" or "cancel"))
                throw new InvalidDataException("Native diagnostic lifecycle owner role is unknown.");
            string receiptPath = owner.GetProperty("receipt").GetString()!;
            DirectoryInfo role = new(Path.GetDirectoryName(receiptPath)!);
            if (Path.GetFileName(receiptPath) != "receipt.json" || role.Name != label || role.Parent?.Name != "hexalith-ci-qualification")
                throw new InvalidDataException("Native diagnostic receipt escaped its existing controller role.");
            if (!File.Exists(receiptPath)) continue; // A sibling launch intent may not yet have an issued receipt.
            using JsonDocument receipt = ReadDocument(receiptPath);
            JsonElement issued = receipt.RootElement;
            if (issued.GetProperty("runId").GetString() != RunId) continue;
            RequireEqual(issued.GetProperty("schema").GetString(), "hexalith.module.ci-owner-receipt.v1");
            RequireEqual(issued.GetProperty("attemptId").GetString(), AttemptId);
            RequireEqual(issued.GetProperty("environmentId").GetString(), EnvironmentId);
            RequireEqual(issued.GetProperty("stateDirectory").GetString(), StateDirectory);
            if (!JsonElement.DeepEquals(ExecutionContext, issued.GetProperty("executionContext")))
                throw new InvalidDataException("Native diagnostic lifecycle receipt context changed.");
            matches++;
        }
        if (matches != 1) throw new InvalidDataException("Native diagnostic lifecycle owner has no unique issued receipt.");
    }

    private static void VerifyMapping(JsonElement mapping, JsonElement plan, string hash)
    {
        string[] fields = ["Mode", "Root", "ActiveModule", "Entries", "HasGit", "PlatformIdentity", "IdentityFindings"];
        var original = fields.ToDictionary(name => name, mapping.GetProperty, StringComparer.Ordinal);
        string calculated = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(original)));
        RequireEqual(calculated, hash);
        RequireEqual(mapping.GetProperty("ContentHash").GetString(), hash);
        RequireEqual(plan.GetProperty("sourceMappingHash").GetString(), hash);
        RequireEqual(plan.GetProperty("sourceMapping").GetProperty("contentHash").GetString(), hash);
        RequireEqual(plan.GetProperty("sourceMapping").GetProperty("root").GetString(), mapping.GetProperty("Root").GetString());
        if (!mapping.GetProperty("HasGit").GetBoolean()) throw new InvalidDataException("Native diagnostic caller requires its real Git root.");
    }

    private static void VerifyContext(JsonElement context)
    {
        string kind = context.GetProperty("kind").GetString()!;
        string[] names = kind == "local" ? ["kind"]
            : ["kind", "repository", "workflowRunId", "workflowAttempt", "jobId", "runnerKind", "operatingSystem", "disposable"];
        if (context.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(names.Order(StringComparer.Ordinal)) is false || kind is not ("local" or "ci"))
            throw new InvalidDataException("Native diagnostic context has unexpected fields.");
        if (kind == "local") return;
        RequireEqual(context.GetProperty("runnerKind").GetString(), "github-hosted");
        RequireEqual(context.GetProperty("operatingSystem").GetString(), "Linux");
        if (!context.GetProperty("disposable").GetBoolean() || context.GetProperty("workflowAttempt").GetInt32() <= 0
            || new[] { "repository", "workflowRunId", "jobId" }.Any(name => string.IsNullOrWhiteSpace(context.GetProperty(name).GetString())))
            throw new InvalidDataException("Native diagnostic CI context is incomplete.");
    }

    private static void VerifyOwner(string runId, string workspace, JsonElement run, JsonElement plan, JsonElement state)
    {
        RequireEqual(state.GetProperty("schema").GetString(), "hexalith.g4-run-state.v1");
        RequireEqual(state.GetProperty("runId").GetString(), runId);
        RequireEqual(state.GetProperty("workspace").GetString(), workspace);
        RequireEqual(run.GetProperty("runId").GetString(), runId);
        RequireEqual(run.GetProperty("ownerRunId").GetString(), runId);
        RequireEqual(run.GetProperty("role").GetString(), "owner");
        foreach (string name in new[] { "attemptId", "environmentId" })
        {
            string value = run.GetProperty(name).GetString()!;
            RequireEqual(state.GetProperty(name).GetString(), value);
            RequireEqual(plan.GetProperty(name).GetString(), value);
        }
    }

    internal JsonElement? ReadCurrentOwnerObservation()
    {
        if (Profile != "platform-current-package") return null;
        string directory = Path.Combine(OwnerWorkspace, "platform-components", EnvironmentId + "-catalog");
        using JsonDocument document = ReadDocument(Path.Combine(directory, "current-owner-observation.json"));
        JsonElement value = document.RootElement;
        foreach (var expected in new[] { ("profile", Profile), ("runId", RunId), ("attemptId", AttemptId),
            ("environmentId", EnvironmentId), ("revision", Revision), ("mappingHash", MappingHash), ("configuration", "Release") })
            RequireEqual(value.GetProperty(expected.Item1).GetString(), expected.Item2);
        if (value.GetProperty("qualificationCredit").GetBoolean() || !value.GetProperty("committedCatalogValidated").GetBoolean())
            throw new InvalidDataException("Current owner observation has no committed baseline authority.");
        return value.Clone();
    }

    private static JsonDocument ReadDocument(string path)
    {
        VerifyDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        FileInfo file = new(path);
        if (!Path.IsPathFullyQualified(path) || file.LinkTarget is not null || file.Length is <= 0 or > 4194304)
            throw new InvalidDataException("Native diagnostic metadata must be bounded regular non-linked files.");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }

    private static void VerifyDirectory(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path)) throw new InvalidDataException("Native diagnostic directory is unavailable.");
        for (DirectoryInfo? parent = new(path); parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null) throw new IOException("Native diagnostic paths cannot traverse links.");
    }

    private static string ReadRevision(string root)
    {
        ProcessStartInfo start = new("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("rev-parse"); start.ArgumentList.Add("--show-toplevel"); start.ArgumentList.Add("HEAD");
        using Process child = Process.Start(start) ?? throw new IOException("Cannot inspect the original caller Git root.");
        Task<string> output = child.StandardOutput.ReadToEndAsync(); Task<string> error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(5000)) { child.Kill(entireProcessTree: true); throw new IOException("Caller Git identity inspection exceeded its finite bound."); }
        _ = error.GetAwaiter().GetResult();
        string[] lines = output.GetAwaiter().GetResult().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (child.ExitCode != 0 || lines.Length != 2 || lines[0] != root || lines[1].Length != 40 || lines[1].Any(value => !char.IsAsciiHexDigit(value)))
            throw new InvalidDataException("Original caller Git identity cannot be verified.");
        return lines[1];
    }

    private static void RequireEqual(string? actual, string? expected)
    {
        if (string.IsNullOrEmpty(actual) || actual != expected) throw new InvalidDataException("Native diagnostic metadata binding mismatch.");
    }
}
