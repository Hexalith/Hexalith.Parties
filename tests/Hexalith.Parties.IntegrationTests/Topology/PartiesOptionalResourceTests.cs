extern alias apphost;

using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Checks optional workload identities and endpoint references on actual Aspire models.</summary>
[Collection("Non-parallel")]
public sealed class PartiesOptionalResourceTests
{
    /// <summary>Optional SDK receivers and search URLs follow their declared Aspire resources.</summary>
    /// <param name="operation">The actual application-model execution mode.</param>
    [Theory]
    [InlineData("run")]
    [InlineData("publish")]
    public async Task OptionalResources_UseTheirSidecarIdentityAndAllocatedMemoriesEndpoint(string operation)
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                [$"--operation={operation}", "--PUBLISH_TARGET=", "--EnableMemoriesSearch=true",
                    "--EnableEventStoreSampleUi=true", "--EnableKeycloak=false",
                    "--PartiesUi:OidcClientSecret=model-only-secret"], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await using (builder.ConfigureAwait(true))
        {
            builder.ExecutionContext.IsRunMode.ShouldBe(operation == "run");
            builder.ExecutionContext.IsPublishMode.ShouldBe(operation == "publish");
            ProjectResource sample = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "sample");
            IDictionary<string, object> sampleEnvironment = await EvaluateEnvironmentAsync(builder, sample).ConfigureAwait(true);
            string sidecarAppId = sample.Annotations.OfType<DaprSidecarAnnotation>().Single()
                .Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId!;
            sidecarAppId.ShouldBe("sample");
            sampleEnvironment["EventStore__DomainService__AppId"].ShouldBe(sidecarAppId);

            ProjectResource memories = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "memories");
            EndpointAnnotation endpoint = memories.Annotations.OfType<EndpointAnnotation>().Single(annotation => annotation.Name == "http");
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", 45821);
            ProjectResource parties = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "parties");
            IDictionary<string, object> environment = await EvaluateEnvironmentAsync(builder, parties).ConfigureAwait(true);
            var searchEndpoint = environment["Parties__MemoriesSearch__Endpoint"].ShouldBeAssignableTo<EndpointReference>();
            searchEndpoint.Resource.ShouldBeSameAs(memories);
            searchEndpoint.EndpointName.ShouldBe("http");
            (await searchEndpoint.GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(true))
                .ShouldBe("http://127.0.0.1:45821");
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.2", 45822);
            (await searchEndpoint.GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(true))
                .ShouldBe("http://127.0.0.2:45822");
            environment["Parties__MemoriesSearch__Enabled"].ShouldBe("true");
            parties.Annotations.OfType<WaitAnnotation>().ShouldContain(wait => ReferenceEquals(wait.Resource, memories));
            if (builder.ExecutionContext.IsPublishMode)
            {
                using var output = new MemoryStream();
                using (var writer = new Utf8JsonWriter(output))
                {
                    writer.WriteStartObject();
                    var manifest = new ManifestPublishingContext(builder.ExecutionContext,
                        Path.Combine(Path.GetTempPath(), "parties-model-manifest.json"), writer, TestContext.Current.CancellationToken);
                    await manifest.WriteEnvironmentVariablesAsync(parties).ConfigureAwait(true);
                    writer.WriteEndObject();
                }
                using JsonDocument published = JsonDocument.Parse(output.ToArray());
                published.RootElement.GetProperty("env").GetProperty("Parties__MemoriesSearch__Endpoint").GetString()
                    .ShouldBe("{memories.bindings.http.url}");
            }
        }
    }

    /// <summary>Evaluates callbacks without starting containers or workloads.</summary>
    private static async Task<IDictionary<string, object>> EvaluateEnvironmentAsync(
        IDistributedApplicationTestingBuilder builder, ProjectResource resource)
    {
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, resource,
            new Dictionary<string, object>(), TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context).ConfigureAwait(true);
        }
        return context.EnvironmentVariables;
    }
}
