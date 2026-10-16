using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Routes every test sidecar to fresh, session-owned Redis before the application starts.</summary>
internal static class PartiesTestRedis
{
    /// <summary>Creates a nonpersistent Redis container and replaces all state/pubsub Redis endpoints.</summary>
    /// <param name="builder">The unstarted test application model.</param>
    /// <returns>The owned Redis resource, whose allocated endpoint is also used to verify activation.</returns>
    internal static IResourceBuilder<ContainerResource> Configure(IDistributedApplicationBuilder builder)
    {
        // Reject unmodelled component directories rather than permit a fallback to shared Redis.
        // The current topology declares every state/pubsub component through the SDK model.
        DaprSidecarAnnotation[] sidecars = builder.Resources
            .SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
            .ToArray();
        foreach (DaprSidecarAnnotation sidecar in sidecars)
        {
            DaprSidecarOptions? options = sidecar.Sidecar.Annotations
                .OfType<DaprSidecarOptionsAnnotation>().SingleOrDefault()?.Options;
            if (options?.ResourcesPaths.Count > 0 || !string.IsNullOrEmpty(options?.RunFile))
            {
                throw new InvalidOperationException(
                    $"Test sidecar '{sidecar.Sidecar.Name}' loads unmodelled components; private Redis routing cannot be proved.");
            }
        }

        if (builder.Resources.OfType<DaprComponentResource>()
            .Any(component => !string.IsNullOrEmpty(component.Options?.LocalPath)))
        {
            // The SDK copies LocalPath components into sidecar resource paths after this
            // model is built, without applying the generated component metadata below.
            throw new InvalidOperationException(
                "File-backed Dapr components cannot prove private Redis routing.");
        }

        var components = builder.Resources.OfType<DaprComponentResource>()
            .Where(component => component.Type.StartsWith("state.", StringComparison.Ordinal)
                || component.Type.StartsWith("pubsub.", StringComparison.Ordinal)
                || component.Type == "pubsub")
            .ToArray();
        if (!components.Any(component => component.Name == "statestore")
            || !components.Any(component => component.Name == "pubsub")
            || components.Any(component => component.Type is not ("state.redis" or "pubsub.redis" or "pubsub")))
        {
            throw new InvalidOperationException("The disposable topology requires modelled Redis state and pub/sub components.");
        }

        IResourceBuilder<ContainerResource> redis = builder
            .AddContainer("fixture-redis", "docker.io/library/redis")
            // Aspire adds the sha256: prefix when resolving the complete image name.
            .WithImageSHA256("ff02b58f971e7d7d156a1267e283fcbbeee91773b6aa36c49dac28ecfe28eadf")
            .WithArgs("redis-server", "--save", string.Empty, "--appendonly", "no")
            .WithEndpoint(name: "tcp", targetPort: 6379, scheme: "tcp");
        EndpointReference endpoint = redis.GetEndpoint("tcp");
        ReferenceExpression address = ReferenceExpression.Create(
            $"{endpoint.Property(EndpointProperty.Host)}:{endpoint.Property(EndpointProperty.Port)}");
        // The Toolkit's value-provider overload emits secretKeyRef metadata but only its
        // secret-parameter overload binds auth.secretStore. Keep the empty password in
        // that supported path so redisHost resolves instead of falling back to localhost.
        IResourceBuilder<ParameterResource> password = builder.AddParameter(
            "fixture-redis-password", string.Empty, secret: true);
        foreach (DaprComponentResource component in components)
        {
            _ = builder.CreateResourceBuilder<IDaprComponentResource>(component.Name)
                .WithMetadata("redisHost", address)
                .WithMetadata("redisPassword", password.Resource);
        }

        foreach (DaprSidecarAnnotation sidecar in sidecars)
        {
            sidecar.Sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["REDIS_HOST"] = address;
                context.EnvironmentVariables["REDIS_PASSWORD"] = string.Empty;
            }));
        }

        foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
        {
            _ = builder.CreateResourceBuilder<ProjectResource>(project.Name).WaitFor(redis);
        }

        return redis;
    }
}
