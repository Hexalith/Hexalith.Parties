extern alias apphost;

using System.Diagnostics;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Aspire.Hosting.Publishing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Parties.Contracts.Authorization;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Checks workload environments on actual run and publish models without starting services.</summary>
[Collection("Non-parallel")]
public sealed class PartiesPublishEnvironmentTests
{
    /// <summary>Every published workload selects production configuration for each supported target.</summary>
    /// <param name="target">The optional deployment target registered in the model.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("docker")]
    [InlineData("k8s")]
    [InlineData("aca")]
    public async Task PublishModel_AllWorkloadsUseProduction(string? target)
    {
        string[] args = ["--operation=publish", $"--PUBLISH_TARGET={target}", "--PartiesUi:OidcClientSecret=model-only-secret"];

        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(args, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        await using (builder.ConfigureAwait(true))
        {
            builder.ExecutionContext.IsPublishMode.ShouldBeTrue();
            builder.Configuration["PUBLISH_TARGET"].ShouldBe(target ?? string.Empty);
            ProjectResource[] projects = builder.Resources.OfType<ProjectResource>().ToArray();
            projects.Select(project => project.Name).Order(StringComparer.Ordinal).ShouldBe(
                new[]
                {
                    "eventstore", "eventstore-admin", "eventstore-admin-ui", "memories", "parties",
                    "parties-mcp", "parties-ui", "sample", "sample-blazor-ui", "tenants",
                });

            foreach (ProjectResource project in projects)
            {
                IDictionary<string, object> environment = await EvaluateEnvironmentAsync(builder.ExecutionContext, project).ConfigureAwait(true);
                environment.ShouldContainKey("ASPNETCORE_ENVIRONMENT", project.Name);
                environment["ASPNETCORE_ENVIRONMENT"].ShouldBe("Production", project.Name);
                environment.ShouldContainKey("DOTNET_ENVIRONMENT", project.Name);
                environment["DOTNET_ENVIRONMENT"].ShouldBe("Production", project.Name);
            }
        }
    }

    /// <summary>The publish model and app manifest retain OIDC settings and secret token annotations.</summary>
    /// <param name="target">The deployment target.</param>
    [Theory]
    [InlineData("docker")]
    [InlineData("k8s")]
    [InlineData("aca")]
    public async Task MemoriesPublishModel_ConfiguresExternalOidcAndSecretTokenAnnotations(string target)
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                ["--operation=publish", $"--PUBLISH_TARGET={target}", "--PartiesUi:OidcClientSecret=model-only-secret",
                 "--Memories:Authentication:Authority=https://identity.example.test/realm", "--Memories:Authentication:Issuer=https://issuer.example.test/realm",
                 "--Memories:Authentication:Audience=memories-api", "--Memories:Authentication:TenantClaimName=" + PartiesClaimTypes.EventStoreTenant], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var builderLifetime = builder.ConfigureAwait(true);
        ProjectResource memories = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "memories");
        IDictionary<string, object> environment = await EvaluateEnvironmentAsync(builder.ExecutionContext, memories).ConfigureAwait(true);
        environment["Authentication__JwtBearer__Authority"].ShouldBe("https://identity.example.test/realm");
        environment["Authentication__JwtBearer__Issuer"].ShouldBe("https://issuer.example.test/realm");
        environment["Authentication__JwtBearer__Audience"].ShouldBe("memories-api");
        environment["Authentication__JwtBearer__RequireHttpsMetadata"].ShouldBe("true");
        environment["Authentication__JwtBearer__SigningKey"].ShouldBe("");
        environment["Authentication__JwtBearer__TenantClaimName"].ShouldBe(PartiesClaimTypes.EventStoreTenant);
        environment["Authentication__JwtBearer__ValidAlgorithms__0"].ShouldBe("RS256");
        environment["Authentication__JwtBearer__ValidAlgorithms__1"].ShouldBe("ES256");
        // This checks model annotations only. The selected Toolkit's actual dapr.v0
        // emission drops these variables; full sidecar publication remains an upstream gate.
        IDaprSidecarResource sidecar = memories.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        var sidecarEnvironment = new EnvironmentCallbackContext(builder.ExecutionContext, sidecar,
            new Dictionary<string, object>(), TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation annotation in sidecar.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(sidecarEnvironment);
        }
        ParameterResource[] parameters = builder.Resources.OfType<ParameterResource>().Where(parameter => parameter.Name.StartsWith("memories-", StringComparison.Ordinal)).ToArray();
        parameters.Length.ShouldBe(2);
        parameters.ShouldAllBe(parameter => parameter.Secret);
        foreach (string key in new[] { "APP_API_TOKEN", "DAPR_API_TOKEN" })
        {
            environment[key].ShouldBeAssignableTo<IManifestExpressionProvider>();
            ((IManifestExpressionProvider)environment[key]).ValueExpression.ShouldBe(((IManifestExpressionProvider)sidecarEnvironment.EnvironmentVariables[key]).ValueExpression);
        }
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            var context = new ManifestPublishingContext(builder.ExecutionContext, Path.Combine(Path.GetTempPath(), "parties-memories-manifest.json"), writer, TestContext.Current.CancellationToken);
            await context.WriteEnvironmentVariablesAsync(memories).ConfigureAwait(true);
            writer.WriteEndObject();
        }
        using JsonDocument emitted = JsonDocument.Parse(output.ToArray());
        JsonElement emittedEnvironment = emitted.RootElement.GetProperty("env");
        emittedEnvironment.GetProperty("APP_API_TOKEN").GetString().ShouldBe("{memories-app-api-token.value}");
        emittedEnvironment.GetProperty("DAPR_API_TOKEN").GetString().ShouldBe("{memories-dapr-api-token.value}");
    }

    /// <summary>Unsupported metadata transport is rejected before a publish model can be emitted.</summary>
    /// <param name="setting">The configured OIDC address.</param>
    [Theory]
    [InlineData("Authority")]
    [InlineData("Issuer")]
    public async Task MemoriesPublishModel_RejectsInsecureOidcMetadata(string setting)
    {
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                    ["--operation=publish", "--PUBLISH_TARGET=", "--PartiesUi:OidcClientSecret=model-only-secret",
                     $"--Memories:Authentication:{setting}=http://identity.example.test/realm"], TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using (builder.ConfigureAwait(true)) { }
        }).ConfigureAwait(true);
    }

    /// <summary>Executes app configuration with an operator-supplied sidecar and real stores; this does not qualify sidecar publication.</summary>
    /// <param name="invalid">The single invalid configuration control, or empty for successful startup.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Authentication__JwtBearer__Authority")]
    [InlineData("Authentication__JwtBearer__Issuer")]
    [InlineData("Authentication__JwtBearer__Audience")]
    [InlineData("APP_API_TOKEN")]
    [InlineData("DAPR_API_TOKEN")]
    [InlineData("stopped-sidecar")]
    public async Task MemoriesProductionStartup_ValidatesAppConfigurationAndMissingInputs(string invalid)
    {
        ArgumentNullException.ThrowIfNull(invalid);
        if (Environment.GetEnvironmentVariable("PARTIES_MEMORIES_PRODUCTION_TESTS") != "true")
        {
            Assert.Skip("Set PARTIES_MEMORIES_PRODUCTION_TESTS=true with the built producer and owned Redis/FalkorDB endpoints to execute Production startup controls.");
        }
        string redis = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_TEST_REDIS").ShouldNotBeNull();
        string graph = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_TEST_FALKORDB").ShouldNotBeNull();
        string producer = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_TEST_SERVER").ShouldNotBeNull();
        File.Exists(producer).ShouldBeTrue("Build the actual selected Memories.Server producer before enabling this qualification.");
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                ["--operation=publish", "--PUBLISH_TARGET=", "--PartiesUi:OidcClientSecret=model-only-secret",
                 "--Parameters:memories-app-api-token=" + Guid.NewGuid().ToString("N"),
                 "--Parameters:memories-dapr-api-token=" + Guid.NewGuid().ToString("N")], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var builderLifetime = builder.ConfigureAwait(true);
        ProjectResource memories = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "memories");
        IDictionary<string, object> environment = await EvaluateEnvironmentAsync(builder.ExecutionContext, memories).ConfigureAwait(true);
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(producer)!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(producer);
        foreach ((string key, object value) in environment.Where(pair => pair.Key.StartsWith("Authentication__", StringComparison.Ordinal)
            || pair.Key is "ASPNETCORE_ENVIRONMENT" or "DOTNET_ENVIRONMENT" or "APP_API_TOKEN" or "DAPR_API_TOKEN"))
        {
            if (value is string literal) { start.Environment[key] = literal; }
            else if (value is IValueProvider provider) { start.Environment[key] = await provider.GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(true); }
        }
        start.Environment["ConnectionStrings__redis"] = redis;
        start.Environment["ConnectionStrings__falkordb"] = graph;
        MemoriesProductionSidecar sidecar = MemoriesProductionSidecar.Start(start, redis);
        bool invalidAppInput = invalid.Length > 0 && invalid != "stopped-sidecar";
        if (invalidAppInput) { start.Environment[invalid] = ""; }
        Process process;
        try { process = Process.Start(start).ShouldNotBeNull(); }
        catch (Exception launchFailure)
        {
            try { await sidecar.DisposeAsync().ConfigureAwait(true); }
            catch (Exception cleanupFailure) { launchFailure.Data["FixtureCleanupFailures"] = cleanupFailure; }
            throw;
        }
        using var processLifetime = process;
        var listening = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var captureCancellation = new CancellationTokenSource();
        async Task<string> CaptureAsync(StreamReader reader)
        {
            var output = new System.Text.StringBuilder();
            while (await reader.ReadLineAsync(captureCancellation.Token).ConfigureAwait(true) is string line)
            {
                output.AppendLine(line);
                var address = System.Text.RegularExpressions.Regex.Match(line, @"Now listening on: (http://127\.0\.0\.1:[0-9]+)");
                if (address.Success) { listening.TrySetResult(address.Groups[1].Value); }
            }
            return output.ToString();
        }
        Task<string> stdout = CaptureAsync(process.StandardOutput);
        Task<string> stderr = CaptureAsync(process.StandardError);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        Exception? original = null;
        try
        {
            if (invalidAppInput)
            {
                await process.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
                process.ExitCode.ShouldNotBe(0);
                await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token).ConfigureAwait(true);
                string diagnostics = await stdout.ConfigureAwait(true) + await stderr.ConfigureAwait(true);
                diagnostics.ShouldContain(invalid.Replace("__", ":", StringComparison.Ordinal).Split(':')[^1]);
            }
            else
            {
                Task completed = await Task.WhenAny(listening.Task, process.WaitForExitAsync(deadline.Token)).WaitAsync(deadline.Token).ConfigureAwait(true);
                completed.ShouldBeSameAs(listening.Task, "The actual Production producer exited before listening.");
                using var client = new HttpClient();
                string address = await listening.Task.ConfigureAwait(true);
                System.Net.HttpStatusCode status;
                do
                {
                    using HttpResponseMessage alive = await client.GetAsync(address + "/alive", deadline.Token).ConfigureAwait(true);
                    status = alive.StatusCode;
                    if (alive.IsSuccessStatusCode) { break; }
                    await Task.Delay(100, deadline.Token).ConfigureAwait(true);
                }
                while (!process.HasExited);
                ((int)status).ShouldBeInRange(200, 299);
                process.HasExited.ShouldBeFalse();
                await sidecar.WaitForOutboundHealthAsync(deadline.Token).ConfigureAwait(true);
                if (invalid == "stopped-sidecar")
                {
                    await sidecar.StopForFailureControlAsync().ConfigureAwait(true);
                    // The earlier app liveness response cannot prove that its
                    // independently owned runtime is still alive now.
                    (await Should.ThrowAsync<InvalidOperationException>(() => sidecar.WaitForOutboundHealthAsync(deadline.Token)).ConfigureAwait(true))
                        .Message.ShouldContain("actual Dapr sidecar exited");
                    process.HasExited.ShouldBeFalse();
                }
            }
        }
        catch (Exception exception) { original = exception; throw; }
        finally
        {
            var failures = new List<Exception>();
            try
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); }
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(true);
            }
            catch (Exception exception) { failures.Add(exception); }
            try
            {
                using var capture = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await Task.WhenAll(stdout, stderr).WaitAsync(capture.Token).ConfigureAwait(true);
            }
            catch (Exception exception) { failures.Add(exception); }
            try { await captureCancellation.CancelAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true); }
            catch (Exception exception) { failures.Add(exception); }
            try { await sidecar.DisposeAsync().ConfigureAwait(true); }
            catch (Exception exception) { failures.Add(exception); }
            if (failures.Count > 0)
            {
                var failure = new AggregateException("Production fixture cleanup failed after independently bounded attempts.", failures);
                if (original is null) { throw failure; }
                original.Data["FixtureCleanupFailures"] = failure;
            }
        }
    }

    /// <summary>Run-mode callbacks leave workload environment selection to the launch configuration.</summary>
    /// <param name="includeOptional">Whether optional Memories and sample resources are included.</param>
    /// <param name="initialEnvironment">An explicit workload environment supplied before callback evaluation.</param>
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "Staging")]
    [InlineData(true, "Staging")]
    public async Task RunModel_DoesNotOverrideWorkloadEnvironments(bool includeOptional, string? initialEnvironment)
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                ["--operation=run", "--PUBLISH_TARGET=", $"--EnableMemoriesSearch={includeOptional}", $"--EnableEventStoreSampleUi={includeOptional}"],
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        await using (builder.ConfigureAwait(true))
        {
            builder.ExecutionContext.IsRunMode.ShouldBeTrue();
            builder.Configuration["PUBLISH_TARGET"].ShouldBe(string.Empty);
            ProjectResource[] projects = builder.Resources.OfType<ProjectResource>().ToArray();
            projects.Length.ShouldBe(includeOptional ? 10 : 7);
            foreach (ProjectResource project in projects)
            {
                IDictionary<string, object> environment = await EvaluateEnvironmentAsync(builder.ExecutionContext, project, initialEnvironment).ConfigureAwait(true);
                environment.ShouldContainKey("ASPNETCORE_ENVIRONMENT", project.Name);
                environment["ASPNETCORE_ENVIRONMENT"].ShouldBe(initialEnvironment ?? "Development", project.Name);
                if (initialEnvironment is null)
                {
                    environment.ShouldNotContainKey("DOTNET_ENVIRONMENT", project.Name);
                }
                else
                {
                    environment["DOTNET_ENVIRONMENT"].ShouldBe(initialEnvironment, project.Name);
                }
            }
        }
    }

    /// <summary>Evaluates configured variables using the operation selected by the actual model.</summary>
    /// <param name="executionContext">The actual run or publish operation.</param>
    /// <param name="project">The workload whose configured environment is inspected.</param>
    /// <param name="initialEnvironment">An optional preconfigured workload environment.</param>
    /// <returns>The environment produced by all workload callbacks.</returns>
    private static async Task<IDictionary<string, object>> EvaluateEnvironmentAsync(
        DistributedApplicationExecutionContext executionContext,
        ProjectResource project,
        string? initialEnvironment = null)
    {
        Dictionary<string, object> environment = [];
        if (initialEnvironment is not null)
        {
            environment["ASPNETCORE_ENVIRONMENT"] = initialEnvironment;
            environment["DOTNET_ENVIRONMENT"] = initialEnvironment;
        }

        var context = new EnvironmentCallbackContext(
            executionContext,
            project,
            environment,
            TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation annotation in project.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context).ConfigureAwait(true);
        }

        return context.EnvironmentVariables;
    }
}
