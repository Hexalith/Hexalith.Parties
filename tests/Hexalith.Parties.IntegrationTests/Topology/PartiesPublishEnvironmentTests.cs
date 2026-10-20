extern alias apphost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

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
