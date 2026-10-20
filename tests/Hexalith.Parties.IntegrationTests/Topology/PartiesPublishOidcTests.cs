extern alias apphost;

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Checks OIDC secret admission on actual AppHost models without starting services.</summary>
[Collection("Non-parallel")]
public sealed class PartiesPublishOidcTests
{
    /// <summary>Publishing fails before producing a model when its OIDC secret is missing or blank.</summary>
    /// <param name="target">The optional deployment target.</param>
    /// <param name="secret">The absent or blank configuration value.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData(null, " ")]
    [InlineData(null, "\t\r\n")]
    [InlineData("docker", null)]
    [InlineData("docker", "")]
    [InlineData("docker", " ")]
    [InlineData("docker", "\t\r\n")]
    [InlineData("k8s", null)]
    [InlineData("k8s", "")]
    [InlineData("k8s", " ")]
    [InlineData("k8s", "\t\r\n")]
    [InlineData("aca", null)]
    [InlineData("aca", "")]
    [InlineData("aca", " ")]
    [InlineData("aca", "\t\r\n")]
    public async Task PublishModel_RejectsMissingOrBlankOidcSecret(string? target, string? secret)
    {
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            IDistributedApplicationTestingBuilder builder =
                await CreateBuilderAsync("publish", target, secret).ConfigureAwait(true);
            await using (builder.ConfigureAwait(true))
            {
            }
        }).ConfigureAwait(true);

        exception.Message.ShouldBe(
            "Publishing requires a non-whitespace PartiesUi:OidcClientSecret. "
            + "Supply it through configuration, for example the PartiesUi__OidcClientSecret environment variable.");
    }

    /// <summary>Valid secrets reach the UI unchanged for every supported publish target.</summary>
    /// <param name="target">The optional deployment target.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("docker")]
    [InlineData("k8s")]
    [InlineData("aca")]
    public async Task PublishModel_ForwardsExactOidcSecret(string? target)
    {
        const string Secret = " model-only-secret\t ";
        IDistributedApplicationTestingBuilder builder =
            await CreateBuilderAsync("publish", target, Secret).ConfigureAwait(true);

        await using (builder.ConfigureAwait(true))
        {
            builder.ExecutionContext.IsPublishMode.ShouldBeTrue();
            builder.Configuration["PUBLISH_TARGET"].ShouldBe(target ?? string.Empty);
            IDictionary<string, object> environment = await EvaluateUiEnvironmentAsync(builder).ConfigureAwait(true);

            environment["Authentication__OpenIdConnect__ClientSecret"].ShouldBe(Secret);
            environment["Authentication__OpenIdConnect__ClientId"].ShouldBe("hexalith-parties-ui");
            environment["Authentication__OpenIdConnect__Audience"].ShouldBe("hexalith-eventstore");
        }
    }

    /// <summary>Local authentication does not require or consume the production client secret.</summary>
    /// <param name="enableKeycloak">Whether the local identity provider is enabled.</param>
    /// <param name="secret">The absent, blank or populated publish-only configuration value.</param>
    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "\t\r\n")]
    [InlineData(true, "unused-production-secret")]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, "\t\r\n")]
    [InlineData(false, "unused-production-secret")]
    public async Task RunModel_PreservesLocalAuthentication(bool enableKeycloak, string? secret)
    {
        IDistributedApplicationTestingBuilder builder =
            await CreateBuilderAsync("run", null, secret, enableKeycloak).ConfigureAwait(true);

        await using (builder.ConfigureAwait(true))
        {
            builder.ExecutionContext.IsRunMode.ShouldBeTrue();
            builder.Configuration["PartiesUi:OidcClientSecret"].ShouldBe(secret);
            IDictionary<string, object> environment = await EvaluateUiEnvironmentAsync(builder).ConfigureAwait(true);

            if (enableKeycloak)
            {
                environment["Authentication__OpenIdConnect__ClientSecret"].ShouldBe("parties-ui-dev-secret");
                environment["Authentication__OpenIdConnect__ClientId"].ShouldBe("hexalith-parties-ui");
                environment["Authentication__OpenIdConnect__Audience"].ShouldBe("hexalith-eventstore");
            }
            else
            {
                environment.ShouldNotContainKey("Authentication__OpenIdConnect__ClientSecret");
                environment.ShouldNotContainKey("Authentication__OpenIdConnect__Authority");
            }
        }
    }

    /// <summary>The documented environment variable is read through the normal configuration providers.</summary>
    [Fact]
    public async Task PublishModel_ReadsOidcSecretFromEnvironment()
    {
        const string Secret = " environment-only-model-secret\t ";
        const string Variable = "PartiesUi__OidcClientSecret";
        string? previous = Environment.GetEnvironmentVariable(Variable);
        try
        {
            Environment.SetEnvironmentVariable(Variable, Secret);
            IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
                .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                    ["--operation=publish", "--PUBLISH_TARGET="], TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            await using (builder.ConfigureAwait(true))
            {
                builder.ExecutionContext.IsPublishMode.ShouldBeTrue();
                IDictionary<string, object> environment = await EvaluateUiEnvironmentAsync(builder).ConfigureAwait(true);
                environment["Authentication__OpenIdConnect__ClientSecret"].ShouldBe(Secret);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, previous);
        }
    }

    /// <summary>Builds an actual operation with deterministic configuration, without launching resources.</summary>
    /// <param name="operation">The run or publish operation.</param>
    /// <param name="target">The optional deployment target.</param>
    /// <param name="secret">The OIDC configuration value, overriding any ambient credential.</param>
    /// <param name="enableKeycloak">Whether the local identity provider is enabled.</param>
    /// <returns>The unstarted testing builder.</returns>
    private static Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(
        string operation,
        string? target,
        string? secret,
        bool enableKeycloak = true) =>
        DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
            [$"--operation={operation}", $"--PUBLISH_TARGET={target}", $"--EnableKeycloak={enableKeycloak}",
                "--EnableMemoriesSearch=false", "--EnableEventStoreSampleUi=false"],
            (_, settings) =>
            {
                // Default providers would be appended after this callback and could
                // replace the test value with a developer's real environment secret.
                settings.DisableDefaults = true;
                settings.Configuration ??= new ConfigurationManager();
                settings.Configuration.AddEnvironmentVariables();
                settings.Configuration.AddCommandLine(settings.Args ?? []);
                settings.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PartiesUi:OidcClientSecret"] = secret,
                });
            },
            TestContext.Current.CancellationToken);

    /// <summary>Evaluates the UI environment using the model's actual operation.</summary>
    /// <param name="builder">The unstarted run or publish model.</param>
    /// <returns>The configured UI environment.</returns>
    private static async Task<IDictionary<string, object>> EvaluateUiEnvironmentAsync(
        IDistributedApplicationTestingBuilder builder)
    {
        ProjectResource ui = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "parties-ui");
        var context = new EnvironmentCallbackContext(
            builder.ExecutionContext,
            ui,
            new Dictionary<string, object>(),
            TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation annotation in ui.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context).ConfigureAwait(true);
        }

        return context.EnvironmentVariables;
    }
}
