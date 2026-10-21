using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Events.Rejections;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Domain;

/// <summary>Exercises real admission scope construction and registered provisioning dispatch.</summary>
public sealed class PartyIdentityBoundaryTests
{
    private const string ActorId = "01HX0000000000000000000001";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    /// <summary>Requires every independently constructed command or query scope field to match.</summary>
    /// <param name="query">Whether admission targets the query rather than command boundary.</param>
    /// <param name="mismatch">The field of the signed evidence changed from the expected scope.</param>
    [Theory]
    [InlineData(false, "none")]
    [InlineData(false, "tenant")]
    [InlineData(false, "domain")]
    [InlineData(false, "aggregate")]
    [InlineData(false, "operation")]
    [InlineData(false, "request")]
    [InlineData(false, "logical")]
    [InlineData(false, "digest")]
    [InlineData(true, "none")]
    [InlineData(true, "tenant")]
    [InlineData(true, "domain")]
    [InlineData(true, "aggregate")]
    [InlineData(true, "operation")]
    [InlineData(true, "request")]
    [InlineData(true, "logical")]
    [InlineData(true, "digest")]
    public void RealAuthorityBindsEveryScopeField(bool query, string mismatch)
    {
        using RSA key = RSA.Create(2048);
        using ServiceProvider services = CreateServices(key);
        using IServiceScope serviceScope = services.CreateScope();
        IPartyIdentityAuthority authority = serviceScope.ServiceProvider.GetRequiredService<IPartyIdentityAuthority>();
        authority.ShouldBeOfType<PartyIdentityAuthority>();
        (CommandEnvelope command, _) = CreateCommand();
        var request = new QueryEnvelope("tenant-a", "party", command.AggregateId, "ResolvePartyIdentity",
            "{\"synthetic\":true}"u8.ToArray(), "query-correlation", "reader");
        byte[] payload = query ? request.Payload : command.Payload;
        var expected = new IdentityAdmissionScope("tenant-a", "party", command.AggregateId,
            query ? "ResolvePartyIdentity" : typeof(ProvisionAgentParty).FullName!,
            query ? "query-correlation" : "provision-message",
            query ? "query-correlation" : "provision-logical", Convert.ToHexString(SHA256.HashData(payload)));
        IdentityAdmissionScope signedScope = mismatch switch
        {
            "tenant" => expected with { TenantId = "tenant-b" },
            "domain" => expected with { Domain = "tenants" },
            "aggregate" => expected with { AggregateId = ActorId },
            "operation" => expected with { Operation = "OtherOperation" },
            "request" => expected with { MessageId = "other-request" },
            "logical" => expected with { LogicalId = "other-logical" },
            "digest" => expected with { PayloadDigest = new string('0', 64) },
            _ => expected,
        };
        IdentityAdmissionEvidence evidence = Evidence(signedScope);
        string proof = Sign(key, evidence);
        command.Extensions![IdentityAdmissionProof.ExtensionKey] = proof;
        request = request with { IdentityAdmissionProof = proof };

        PartyIdentityAdmissionResult result = query ? authority.Admit(request) : authority.Admit(command);

        result.Evidence.ShouldBe(mismatch == "none" ? evidence : null);
        result.FailureReason.ShouldBe(mismatch == "none" ? null : "identity-authority-unavailable");
    }

    /// <summary>Rejects absent, cryptographically forged and out-of-time proofs at both boundaries.</summary>
    /// <param name="query">Whether admission targets a query.</param>
    /// <param name="scenario">The invalid proof condition.</param>
    [Theory]
    [InlineData(false, "missing")]
    [InlineData(false, "malformed")]
    [InlineData(false, "foreign-key")]
    [InlineData(false, "expired")]
    [InlineData(false, "future-issued")]
    [InlineData(true, "missing")]
    [InlineData(true, "malformed")]
    [InlineData(true, "foreign-key")]
    [InlineData(true, "expired")]
    [InlineData(true, "future-issued")]
    public void RealAuthorityRejectsMissingOrForgedProof(bool query, string scenario)
    {
        using RSA key = RSA.Create(2048);
        using ServiceProvider services = CreateServices(key);
        using IServiceScope scope = services.CreateScope();
        IPartyIdentityAuthority authority = scope.ServiceProvider.GetRequiredService<IPartyIdentityAuthority>();
        (CommandEnvelope command, _) = CreateCommand();
        var request = new QueryEnvelope("tenant-a", "party", command.AggregateId, "ResolvePartyIdentity",
            "{}"u8.ToArray(), "query-correlation", "reader");
        IdentityAdmissionScope expected = query
            ? new("tenant-a", "party", command.AggregateId, "ResolvePartyIdentity", "query-correlation",
                "query-correlation", Convert.ToHexString(SHA256.HashData(request.Payload)))
            : CommandScope(command);
        IdentityAdmissionEvidence evidence = scenario switch
        {
            "expired" => Evidence(expected) with { IssuedAt = Now.AddMinutes(-1), ExpiresAt = Now },
            "future-issued" => Evidence(expected) with { IssuedAt = Now.AddSeconds(1) },
            _ => Evidence(expected),
        };
        using RSA foreignKey = RSA.Create(2048);
        string? proof = scenario switch
        {
            "missing" => null,
            "malformed" => "forged-proof",
            "foreign-key" => Sign(foreignKey, evidence),
            _ => Sign(key, evidence),
        };
        if (proof is not null)
        {
            command.Extensions![IdentityAdmissionProof.ExtensionKey] = proof;
        }

        request = request with { IdentityAdmissionProof = proof };

        PartyIdentityAdmissionResult result = query ? authority.Admit(request) : authority.Admit(command);

        result.Evidence.ShouldBeNull();
        result.FailureReason.ShouldBe("identity-authority-unavailable");
    }

    /// <summary>Malformed logical identity cannot escape the authority's fail-closed boundary.</summary>
    /// <param name="payload">A JSON payload without a usable logical identity.</param>
    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"logicalId\":42}")]
    public void RealAuthorityRejectsMalformedCommandPayload(string payload)
    {
        using RSA key = RSA.Create(2048);
        using ServiceProvider services = CreateServices(key);
        using IServiceScope scope = services.CreateScope();
        (CommandEnvelope command, _) = CreateCommand();
        command = command with { Payload = System.Text.Encoding.UTF8.GetBytes(payload) };
        command.Extensions![IdentityAdmissionProof.ExtensionKey] = Sign(key, Evidence(CommandScope(command)));

        PartyIdentityAdmissionResult result = scope.ServiceProvider.GetRequiredService<IPartyIdentityAuthority>().Admit(command);

        result.Evidence.ShouldBeNull();
        result.FailureReason.ShouldBe("identity-authority-unavailable");
    }

    /// <summary>Registered dispatch injects trusted authorization and preserves the original result on retry.</summary>
    [Fact]
    public async Task RegisteredProcessorProvisionsAndReturnsTheOriginalIdentityOnRetryAsync()
    {
        using RSA key = RSA.Create(2048);
        using ServiceProvider services = CreateServices(key);
        using IServiceScope scope = services.CreateScope();
        IAsyncDomainProcessor processor = scope.ServiceProvider.GetRequiredKeyedService<IAsyncDomainProcessor>("party");
        processor.ShouldBeOfType<PartyDomainProcessor>();
        (CommandEnvelope command, AgentPartyIdentity intent) = CreateCommand();
        JsonSerializer.Deserialize<ProvisionAgentParty>(command.Payload, PartiesJsonOptions.Default)!.Authorization.ShouldBeNull();
        command.Extensions![IdentityAdmissionProof.ExtensionKey] = Sign(key, Evidence(CommandScope(command)));

        DomainResult result = await processor.ProcessAsync(command, null, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeFalse();
        result.Events.Select(item => item.GetType()).ShouldBe(
            [typeof(PartyCreated), typeof(PartyDisplayNameDerived), typeof(AgentPartyProvisioned)]);
        PartyCreated created = result.Events[0].ShouldBeOfType<PartyCreated>();
        created.Type.ShouldBe(PartyType.Organization);
        created.CreatedAt.ShouldBe(intent.CreatedAt);
        created.OrganizationDetails!.LegalName.ShouldBe("Agent");
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>().DisplayName.ShouldBe("Agent");
        var expected = new AgentPartyProvisioningResult(intent, 3, "trusted-provisioner");
        result.Events[2].ShouldBeOfType<AgentPartyProvisioned>().Result.ShouldBe(expected);
        JsonSerializer.Deserialize<AgentPartyProvisioningResult>(result.ResultPayload!, PartiesJsonOptions.Default).ShouldBe(expected);

        var state = new PartyState();
        state.Apply(created);
        state.Apply(result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>());
        state.Apply(result.Events[2].ShouldBeOfType<AgentPartyProvisioned>());
        state.AgentProvisioning.ShouldBe(expected);
        state.Apply(new PartyDisplayNameDerived { DisplayName = "Renamed Agent", SortName = "Renamed Agent" });
        // A fresh transport request retains the immutable logical provisioning intent.
        CommandEnvelope retry = command with { MessageId = "retry-message", UserId = "retry-transport-user", Extensions = new Dictionary<string, string>() };
        retry.Extensions[IdentityAdmissionProof.ExtensionKey] = Sign(key,
            Evidence(CommandScope(retry)) with { SourceId = "retry-provisioner" });
        IEventPayloadProtectionService protection = services.GetRequiredService<IEventPayloadProtectionService>();
        protection.UnprotectSnapshotStateAsync(retry.AggregateIdentity, state, Arg.Any<CancellationToken>()).Returns(state);

        DomainResult repeated = await processor.ProcessAsync(retry,
            new DomainServiceCurrentState(state, [], 4, 4), TestContext.Current.CancellationToken);

        repeated.IsRejection.ShouldBeFalse();
        repeated.Events.ShouldBeEmpty();
        repeated.ResultPayload.ShouldBe(result.ResultPayload);
        state.AgentProvisioning.ShouldBe(expected);
        state.DisplayName.ShouldBe("Renamed Agent");
    }

    /// <summary>Unsigned and differently targeted provisioning cannot emit creation events or result identity.</summary>
    /// <param name="foreign">Whether to attach validly signed evidence for a different tenant.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisteredProcessorRejectsMissingOrForeignAdmissionAsync(bool foreign)
    {
        using RSA key = RSA.Create(2048);
        using ServiceProvider services = CreateServices(key);
        using IServiceScope scope = services.CreateScope();
        IAsyncDomainProcessor processor = scope.ServiceProvider.GetRequiredKeyedService<IAsyncDomainProcessor>("party");
        (CommandEnvelope command, _) = CreateCommand();
        if (foreign)
        {
            command.Extensions![IdentityAdmissionProof.ExtensionKey] = Sign(key,
                Evidence(CommandScope(command) with { TenantId = "tenant-b" }));
        }

        var protectedSource = new DomainServiceCurrentState(new { marker = "protected-snapshot" }, [], 1, 1);
        DomainResult result = await processor.ProcessAsync(command, protectedSource, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>()
            .Failures.Single().ErrorCode.ShouldBe("AuthorityUnavailable");
        result.ResultPayload.ShouldBeNull();
        IEventPayloadProtectionService protection = services.GetRequiredService<IEventPayloadProtectionService>();
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    private static ServiceProvider CreateServices(RSA key)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentityAdmission:VerificationKeyPem"] = key.ExportSubjectPublicKeyInfoPem(),
            ["IdentityAdmission:AuthorityRevision"] = "1",
            ["Tenants:PubSubName"] = "pubsub",
            ["Tenants:TopicName"] = "system.tenants.events",
        }).Build();
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(clock);
        services.AddSingleton(Substitute.For<DaprClient>());
        services.AddParties(configuration);
        services.AddSingleton(Substitute.For<IEventPayloadProtectionService>());
        return services.BuildServiceProvider();
    }

    private static (CommandEnvelope Command, AgentPartyIdentity Identity) CreateCommand()
    {
        string partyId = AgentPartyIdMapping.Create("tenant-a", ActorId);
        var identity = new AgentPartyIdentity("tenant-a", ActorId, partyId, 1, "provision-logical", new string('a', 64), Now);
        var command = new CommandEnvelope("provision-message", "tenant-a", "party", partyId, typeof(ProvisionAgentParty).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(new ProvisionAgentParty(identity), PartiesJsonOptions.Default),
            "command-correlation", null, "transport-user", new Dictionary<string, string>());
        return (command, identity);
    }

    private static IdentityAdmissionScope CommandScope(CommandEnvelope command)
        => new(command.TenantId, command.Domain, command.AggregateId, command.CommandType, command.MessageId,
            "provision-logical", Convert.ToHexString(SHA256.HashData(command.Payload)));

    private static IdentityAdmissionEvidence Evidence(IdentityAdmissionScope scope)
        => new(scope, "trusted-provisioner", ActorId, ActorId, 1, true, Now, Now.AddMinutes(1), 1);

    private static string Sign(RSA key, IdentityAdmissionEvidence evidence)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(evidence);
        byte[] signature = key.SignData(IdentityAdmissionProof.Encode(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return Convert.ToBase64String(payload) + "." + Convert.ToBase64String(signature);
    }
}
