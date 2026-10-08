using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Commands;
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
using Hexalith.Parties.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Tests.Domain;

/// <summary>Verifies identity authority, retention policy and cancellation at command dispatch.</summary>
public sealed class PartyIdentityAdmissionTests
{
    private const string ActorId = "01HX0000000000000000000001";
    private static readonly DateTimeOffset EffectiveAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z");

    [Theory]
    [InlineData(null)]
    [InlineData("unsupported")]
    [InlineData("binding-closure")]
    public async Task MissingOrUnsupportedPolicy_DeniesBeforeAnyProtectedUnwrap(string? trigger)
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        IPartyIdentityAuthority authority = Substitute.For<IPartyIdentityAuthority>();
        authority.Admit(Arg.Any<CommandEnvelope>()).Returns(new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Bind", "message", "logical", "digest"),
            "writer", "01HX0000000000000000000001", "01HX0000000000000000000001", 1, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), null));
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        IOptionsMonitor<PartyIdentityOptions> options = Substitute.For<IOptionsMonitor<PartyIdentityOptions>>();
        options.CurrentValue.Returns(new PartyIdentityOptions { PolicyId = "synthetic", Retention = TimeSpan.FromDays(1), ExpiryTrigger = trigger });
        var processor = new PartyDomainProcessor(protection, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<PartyDomainProcessor>.Instance,
            identityAuthority: authority, identityHistoryCustody: Substitute.For<IIdentityHistoryCustody>(),
            identityOptions: options);
        var payload = new EstablishHumanActorBinding("tenant-a", "party-1", "01HX0000000000000000000001", 1, 1, 0, DateTimeOffset.UtcNow, "logical", "synthetic");
        var command = new CommandEnvelope("01HX0000000000000000000001", "tenant-a", "party", "party-1", typeof(EstablishHumanActorBinding).FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, PartiesJsonOptions.Default), "correlation", null, "writer", null);
        (await processor.ProcessAsync(command, new DomainServiceCurrentState(new { marker = "protected" }, [], 1, 1), TestContext.Current.CancellationToken)).IsRejection.ShouldBeTrue();
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    /// <summary>Rejects admission that expires or changes while a protected-state or custody provider is awaited.</summary>
    /// <param name="operation">The identity command under test.</param>
    /// <param name="provider">The provider whose response is suspended.</param>
    /// <param name="change">The admission invalidation applied during the wait.</param>
    [Theory]
    [InlineData("provision", "unprotect", "expired")]
    [InlineData("establish", "unprotect", "expired")]
    [InlineData("rebind", "unprotect", "expired")]
    [InlineData("revoke", "unprotect", "expired")]
    [InlineData("provision", "unprotect", "trust-withdrawn")]
    [InlineData("establish", "unprotect", "trust-withdrawn")]
    [InlineData("rebind", "unprotect", "trust-withdrawn")]
    [InlineData("revoke", "unprotect", "trust-withdrawn")]
    [InlineData("establish", "custody", "expired")]
    [InlineData("rebind", "custody", "expired")]
    [InlineData("revoke", "custody", "expired")]
    [InlineData("establish", "custody", "trust-withdrawn")]
    [InlineData("rebind", "custody", "trust-withdrawn")]
    [InlineData("revoke", "custody", "trust-withdrawn")]
    [InlineData("establish", "custody", "actor-inactive")]
    [InlineData("rebind", "custody", "actor-inactive")]
    [InlineData("revoke", "custody", "actor-inactive")]
    public Task AdmissionChangedDuringProviderWait_DeniesIdentityDispatch(string operation, string provider, string change)
        => VerifySuspendedCommandAsync(operation, provider, change);

    /// <summary>Observes real configuration reloads and rejects a withdrawn or changed retention policy.</summary>
    /// <param name="operation">The binding command under test.</param>
    /// <param name="provider">The provider whose response is suspended.</param>
    /// <param name="change">The policy reload applied during the wait.</param>
    [Theory]
    [InlineData("establish", "unprotect", "policy-withdrawn")]
    [InlineData("rebind", "unprotect", "policy-withdrawn")]
    [InlineData("revoke", "unprotect", "policy-withdrawn")]
    [InlineData("establish", "custody", "policy-withdrawn")]
    [InlineData("rebind", "custody", "policy-withdrawn")]
    [InlineData("revoke", "custody", "policy-withdrawn")]
    [InlineData("establish", "custody", "policy-changed")]
    [InlineData("rebind", "custody", "policy-changed")]
    [InlineData("revoke", "custody", "policy-changed")]
    public Task PolicyReloadDuringProviderWait_DeniesBindingDispatch(string operation, string provider, string change)
        => VerifySuspendedCommandAsync(operation, provider, change);

    /// <summary>Preserves caller cancellation when a noncooperative provider returns after cancellation.</summary>
    /// <param name="operation">The identity command under test.</param>
    /// <param name="provider">The provider whose response is suspended.</param>
    [Theory]
    [InlineData("provision", "unprotect")]
    [InlineData("establish", "unprotect")]
    [InlineData("rebind", "unprotect")]
    [InlineData("revoke", "unprotect")]
    [InlineData("establish", "custody")]
    [InlineData("rebind", "custody")]
    [InlineData("revoke", "custody")]
    public Task ProviderReturnsAfterCallerCancellation_DoesNotDispatchIdentityCommand(string operation, string provider)
        => VerifySuspendedCommandAsync(operation, provider, "cancelled");

    /// <summary>Rejects dispatch when the final authority callback cancels but returns unchanged valid evidence.</summary>
    /// <param name="operation">The identity command under test.</param>
    /// <param name="provider">The provider whose response is suspended.</param>
    [Theory]
    [InlineData("provision", "unprotect")]
    [InlineData("establish", "unprotect")]
    [InlineData("rebind", "unprotect")]
    [InlineData("revoke", "unprotect")]
    [InlineData("establish", "custody")]
    [InlineData("rebind", "custody")]
    [InlineData("revoke", "custody")]
    public Task FinalAuthorityCheckCancelsCaller_DoesNotDispatchIdentityCommand(string operation, string provider)
        => VerifySuspendedCommandAsync(operation, provider, "cancel-on-final-verification");

    /// <summary>Preserves all identity operations when their admitted evidence remains valid through the wait.</summary>
    /// <param name="operation">The identity command under test.</param>
    /// <param name="provider">The provider whose response is suspended.</param>
    [Theory]
    [InlineData("provision", "unprotect")]
    [InlineData("establish", "unprotect")]
    [InlineData("rebind", "unprotect")]
    [InlineData("revoke", "unprotect")]
    [InlineData("establish", "custody")]
    [InlineData("rebind", "custody")]
    [InlineData("revoke", "custody")]
    public Task UnchangedAdmissionAfterProviderWait_PreservesIdentityCommand(string operation, string provider)
        => VerifySuspendedCommandAsync(operation, provider, "unchanged");

    private static async Task VerifySuspendedCommandAsync(string operation, string provider, string change)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Parties:Identity:PolicyId"] = "synthetic",
            ["Parties:Identity:Retention"] = "10.00:00:00",
            ["Parties:Identity:ExpiryTrigger"] = "binding-effective-at",
        }).Build();
        var registrations = new ServiceCollection();
        registrations.AddOptions<PartyIdentityOptions>().Bind(configuration.GetSection("Parties:Identity"));
        using ServiceProvider services = registrations.AddValidatorsFromAssemblyContaining<ProvisionAgentPartyValidator>().BuildServiceProvider();
        IOptionsMonitor<PartyIdentityOptions> identityOptions = services.GetRequiredService<IOptionsMonitor<PartyIdentityOptions>>();
        PartyIdentityOptions settings = identityOptions.CurrentValue;
        IdentityHistoryPolicy policy = settings.Policy!;
        var custodyEvidence = new IdentityHistoryCustodyEvidence(policy.PolicyId, "party-actor-history-v1",
            EffectiveAt.Add(policy.Retention), 1, "custody", true, true, true);
        (CommandEnvelope command, PartyState snapshot, long position) = CreateIdentityCommand(operation, policy);
        var source = new DomainServiceCurrentState(snapshot, [], position, position);
        var scope = new IdentityAdmissionScope(command.TenantId, command.Domain, command.AggregateId,
            command.CommandType, command.MessageId, "logical", IdentityAdmissionProof.Digest(command.Payload));
        var admitted = new IdentityAdmissionEvidence(scope, "trusted-writer", ActorId, ActorId, 1, true,
            EffectiveAt, EffectiveAt.AddMinutes(1), 1);
        using RSA signingKey = RSA.Create(2048);
        command.Extensions![IdentityAdmissionProof.ExtensionKey] = Sign(signingKey, admitted);
        var trust = new IdentityAdmissionOptions { VerificationKeyPem = signingKey.ExportSubjectPublicKeyInfoPem(), AuthorityRevision = 1 };
        IOptionsMonitor<IdentityAdmissionOptions> trustOptions = Substitute.For<IOptionsMonitor<IdentityAdmissionOptions>>();
        trustOptions.CurrentValue.Returns(trust);
        DateTimeOffset now = EffectiveAt;
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_ => now);
        var verifiedAuthority = new PartyIdentityAuthority(new IdentityAdmissionProof(trustOptions, clock));
        verifiedAuthority.Admit(command).Evidence.ShouldBe(admitted);
        int verificationCount = 0;
        IPartyIdentityAuthority authority = Substitute.For<IPartyIdentityAuthority>();
        authority.Admit(Arg.Any<CommandEnvelope>()).Returns(call =>
        {
            PartyIdentityAdmissionResult result = verifiedAuthority.Admit(call.Arg<CommandEnvelope>());
            if (++verificationCount == 3 && change == "cancel-on-final-verification")
            {
                caller.Cancel();
            }

            return result;
        });

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection.UnprotectSnapshotStateAsync(command.AggregateIdentity, snapshot, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (provider == "unprotect")
                {
                    entered.SetResult();
                    await release.Task.ConfigureAwait(false);
                }

                return (object)snapshot;
            });
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        custody.AdmitAsync(command.AggregateIdentity, policy, EffectiveAt, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (provider == "custody")
                {
                    entered.SetResult();
                    await release.Task.ConfigureAwait(false);
                }

                return (IdentityHistoryCustodyEvidence?)custodyEvidence;
            });
        var processor = new PartyDomainProcessor(protection, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PartyDomainProcessor>.Instance, identityAuthority: authority, identityHistoryCustody: custody,
            identityOptions: identityOptions);
        Task<DomainResult> pending = processor.ProcessAsync(command, source, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(false);
            pending.IsCompleted.ShouldBeFalse();
            switch (change)
            {
                case "expired":
                    now = admitted.ExpiresAt;
                    verifiedAuthority.Admit(command).Evidence.ShouldBeNull();
                    break;
                case "trust-withdrawn":
                    trust.AuthorityRevision++;
                    verifiedAuthority.Admit(command).Evidence.ShouldBeNull();
                    break;
                case "actor-inactive":
                    command.Extensions![IdentityAdmissionProof.ExtensionKey] = Sign(signingKey, admitted with { ActorActive = false });
                    verifiedAuthority.Admit(command).Evidence!.ActorActive.ShouldBeFalse();
                    break;
                case "policy-withdrawn":
                    configuration["Parties:Identity:PolicyId"] = null;
                    configuration.Reload();
                    identityOptions.CurrentValue.Policy.ShouldBeNull();
                    settings.Policy.ShouldBe(policy);
                    break;
                case "policy-changed":
                    configuration["Parties:Identity:Retention"] = "20.00:00:00";
                    configuration.Reload();
                    identityOptions.CurrentValue.Policy.ShouldNotBe(policy);
                    settings.Policy.ShouldBe(policy);
                    break;
                case "cancelled":
                    await caller.CancelAsync().ConfigureAwait(false);
                    break;
            }
        }
        finally
        {
            release.TrySetResult();
        }

        if (change is "cancelled" or "cancel-on-final-verification")
        {
            OperationCanceledException cancelled = await Should.ThrowAsync<OperationCanceledException>(
                () => pending.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).ConfigureAwait(false);
            cancelled.CancellationToken.ShouldBe(caller.Token);
            if (change == "cancel-on-final-verification")
            {
                verificationCount.ShouldBe(3);
            }
        }
        else
        {
            DomainResult result = await pending.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(false);
            if (change == "unchanged")
            {
                result.IsRejection.ShouldBeFalse();
                result.Events.ShouldContain(item => operation == "provision" ? item is AgentPartyProvisioned
                    : operation == "establish" ? item is HumanActorBindingEstablished
                    : operation == "rebind" ? item is HumanActorBindingRebound : item is HumanActorBindingRevoked);
            }
            else
            {
                result.IsRejection.ShouldBeTrue();
                result.Events.ShouldAllBe(item => item is PartyCommandValidationRejected);
                result.Events.Single().ShouldBeOfType<PartyCommandValidationRejected>().Failures.Single().ErrorCode
                    .ShouldBe(change is "policy-withdrawn" or "policy-changed" ? "PolicyUnavailable" : "AuthorityUnavailable");
                result.ResultPayload.ShouldBeNull();
                if (provider == "unprotect")
                {
                    await custody.DidNotReceiveWithAnyArgs().AdmitAsync(default!, default!, default, default).ConfigureAwait(false);
                }
            }
        }

        snapshot.HumanBindingVersion.ShouldBe(operation is "rebind" or "revoke" ? 1 : 0);
        snapshot.AgentProvisioning.ShouldBeNull();
    }

    private static (CommandEnvelope Command, PartyState State, long Position) CreateIdentityCommand(string operation, IdentityHistoryPolicy policy)
    {
        string partyId = operation == "provision" ? AgentPartyIdMapping.Create("tenant-a", ActorId) : "party-1";
        var state = new PartyState();
        long position = 1;
        if (operation != "provision")
        {
            state.Apply(new PartyCreated { Type = PartyType.Person, CreatedAt = EffectiveAt.AddDays(-1),
                PersonDetails = new PersonDetails { FirstName = "Private", LastName = "Name" } });
        }

        if (operation is "rebind" or "revoke")
        {
            DateTimeOffset first = EffectiveAt.AddHours(-1);
            var custody = new IdentityHistoryCustodyEvidence(policy.PolicyId, "party-actor-history-v1", first.Add(policy.Retention), 1,
                "original-custody", true, true, true);
            var evidence = new HumanActorBindingEvidence("tenant-a", partyId, ActorId, 1, 1, first, custody.ExpiresAt,
                "trusted-writer", ActorId, custody);
            state.Apply(new HumanActorBindingEstablished(new(evidence, "original-logical", "original-digest"), first, 0));
            position = 2;
        }

        object payload = operation switch
        {
            "provision" => new ProvisionAgentParty(new("tenant-a", ActorId, partyId, 1, "logical", new string('a', 64), EffectiveAt)),
            "establish" => new EstablishHumanActorBinding("tenant-a", partyId, ActorId, 1, position, 0, EffectiveAt, "logical", policy.PolicyId),
            "rebind" => new RebindHumanActorBinding("tenant-a", partyId, ActorId, 1, position, 1, EffectiveAt, "logical", policy.PolicyId),
            "revoke" => new RevokeHumanActorBinding("tenant-a", partyId, ActorId, 1, position, 1, EffectiveAt, "logical", policy.PolicyId),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        var command = new CommandEnvelope(ActorId, "tenant-a", "party", partyId, payload.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default), "correlation", null,
            "trusted-writer", []);
        return (command, state, position);
    }

    private static string Sign(RSA key, IdentityAdmissionEvidence evidence)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(evidence);
        byte[] signature = key.SignData(IdentityAdmissionProof.Encode(bytes), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return Convert.ToBase64String(bytes) + "." + Convert.ToBase64String(signature);
    }
}
