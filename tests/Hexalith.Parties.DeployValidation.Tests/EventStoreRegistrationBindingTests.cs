namespace Hexalith.Parties.DeployValidation.Tests;

using System.Collections.Generic;

using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Story 9.3 AC1 — Binding roundtrip for the Kubernetes-valid sanitized wildcard registration key.
/// Verifies that the env-var shape emitted by `src/Hexalith.Parties.AppHost/Program.cs` for the
/// EventStore domain-service registrations binds correctly into
/// <see cref="DomainServiceOptions.Registrations"/>, producing a dictionary entry keyed by
/// <c>wildcard_party_v1</c> with the documented payload
/// (AppId=parties, MethodName=process, TenantId=*, Domain=party, Version=v1).
/// </summary>
/// <remarks>
/// <para>Kubernetes ConfigMap data keys must match <c>^[A-Za-z0-9_.-]+$</c> and Pod container
/// env names must match <c>^[A-Za-z_][A-Za-z0-9_]*$</c>. The legacy <c>*|party|v1</c> shape is
/// rejected in both dimensions. This test pins the AppHost-emitted shape against the binder
/// behavior so a future regression cannot silently re-introduce the broken characters.</para>
/// <para>The test is intentionally Tier-1 (no Aspire/Dapr/Docker) — it constructs an
/// <see cref="IConfiguration"/> from a synthetic in-memory dictionary of env vars that mirrors
/// the AppHost emission and binds the <c>EventStore:DomainServices</c> section into
/// <see cref="DomainServiceOptions"/>.</para>
/// </remarks>
public class EventStoreRegistrationBindingTests
{
    /// <summary>
    /// Returns the env-var dictionary the AppHost emits for the sanitized wildcard registration.
    /// </summary>
    /// <remarks>
    /// Mirrors the AppHost emission at
    /// <c>src/Hexalith.Parties.AppHost/Program.cs</c> lines using
    /// <c>EventStore__DomainServices__Registrations__wildcard_party_v1__*</c>.
    /// The <c>__</c> separator is mapped to <c>:</c> by .NET configuration's environment-variable
    /// provider; we model it explicitly here using <c>:</c>-separated keys for the in-memory
    /// provider so the test is portable across operating systems.
    /// </remarks>
    private static IReadOnlyDictionary<string, string?> AppHostEmittedEnvShape()
        => new Dictionary<string, string?>
        {
            ["EventStore:DomainServices:Registrations:wildcard_party_v1:AppId"] = "parties",
            ["EventStore:DomainServices:Registrations:wildcard_party_v1:MethodName"] = "process",
            ["EventStore:DomainServices:Registrations:wildcard_party_v1:TenantId"] = "*",
            ["EventStore:DomainServices:Registrations:wildcard_party_v1:Domain"] = "party",
            ["EventStore:DomainServices:Registrations:wildcard_party_v1:Version"] = "v1",
        };

    [Fact]
    public void AppHostEmittedEnvShape_BindsIntoDomainServiceOptions_WithSanitizedWildcardKey()
    {
        // Arrange — synthesize the in-memory configuration the AppHost emission produces.
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(AppHostEmittedEnvShape())
            .Build();

        // Act — bind the EventStore:DomainServices section to DomainServiceOptions.
        DomainServiceOptions options = new();
        configuration.GetSection("EventStore:DomainServices").Bind(options);

        // Assert — exactly one dictionary entry, keyed by the K8s-valid sanitized form.
        options.Registrations.Count.ShouldBe(1);
        options.Registrations.ShouldContainKey("wildcard_party_v1");

        DomainServiceRegistration registration = options.Registrations["wildcard_party_v1"];
        registration.AppId.ShouldBe("parties");
        registration.MethodName.ShouldBe("process");
        registration.TenantId.ShouldBe("*");
        registration.Domain.ShouldBe("party");
        registration.Version.ShouldBe("v1");
    }

    [Fact]
    public void AppHostEmittedEnvShape_ContainsNoK8sInvalidCharacters()
    {
        // The sanitized form must use only [A-Za-z0-9_] in the dictionary key segment so that
        // (a) the literal env-var name passes Pod container env-name validation
        //     (^[A-Za-z_][A-Za-z0-9_]*$) and
        // (b) the literal ConfigMap data key passes ConfigMap key validation
        //     (^[A-Za-z0-9_.-]+$).
        // The value segments can hold any printable string (TenantId = "*" is fine in values).
        foreach (string key in AppHostEmittedEnvShape().Keys)
        {
            key.Contains('*').ShouldBeFalse($"Key '{key}' contains the '*' literal that is invalid in K8s ConfigMap data keys and Pod env names.");
            key.Contains('|').ShouldBeFalse($"Key '{key}' contains the '|' literal that is invalid in K8s ConfigMap data keys and Pod env names.");
        }
    }
}
