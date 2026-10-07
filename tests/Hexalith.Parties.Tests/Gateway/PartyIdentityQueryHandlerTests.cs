using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Queries;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

public sealed class PartyIdentityQueryHandlerTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow.AddDays(-1);
    private const string Actor = "01HX0000000000000000000001";
    private static HumanActorBindingEvidence Binding() => new("tenant-a", "party-1", Actor, 1, 1, Start, Start.AddDays(10), "writer", Actor,
        new("synthetic", "party-actor-history-v1", Start.AddDays(10), 1, "custody", true, true, true));
    private static IEventPayload[] Events() => [new PartyCreated { Type = PartyType.Person, CreatedAt = Start, PersonDetails = new PersonDetails { FirstName = "Private", LastName = "Name" } },
        new HumanActorBindingEstablished(new(Binding(), "logical", "digest"), Start, 0)];
    private static QueryEnvelope Envelope<T>(T query) => new("tenant-a", "party", "party-1", typeof(T).FullName!, JsonSerializer.SerializeToUtf8Bytes(query, PartiesJsonOptions.Default), "correlation", "reader", "party-1");
    private static (PartyIdentityQueryService Service, IPartyIdentityAuthority Authority, IAuthoritativeEventStreamReader Reader, IIdentityHistoryCustody Custody, IRetainedIdentityHistoryReader HistoryReader, IOptionsMonitor<PartyIdentityOptions> Options) Service(params IEventPayload[] events)
    {
        IPartyIdentityAuthority authority = Substitute.For<IPartyIdentityAuthority>();
        authority.Admit(Arg.Any<QueryEnvelope>()).Returns(call => new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Read", "correlation", "correlation", "digest"),
            "reader", null, Actor, 1, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), null));
        IAuthoritativeEventStreamReader reader = Substitute.For<IAuthoritativeEventStreamReader>();
        StreamReadEvent[] source = [.. events.Select((item, index) => new StreamReadEvent(index + 1, item.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(item, item.GetType(), PartiesJsonOptions.Default), "json", 1, "message", null, null, Start, "writer"))];
        reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(new(new("tenant-a", "party", "party-1"), source.Length, DateTimeOffset.UtcNow, source, "observation"), null));
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>()).Returns(true);
        IRetainedIdentityHistoryReader historyReader = Substitute.For<IRetainedIdentityHistoryReader>();
        StreamReadEvent[] retained = [.. source.Where((item, index) => events[index] is IIdentityHistoryEvent)
            .Select(item => item with { MessageId = string.Empty, UserId = null, CorrelationId = null, CausationId = null,
                ProtectionMetadata = EventStorePayloadProtectionMetadata.Unprotected() })];
        long[] excluded = [.. source.Where((item, index) => events[index] is not IIdentityHistoryEvent).Select(item => item.SequenceNumber)];
        historyReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RetainedIdentityHistoryReadResult(new(new("tenant-a", "party", "party-1"), RetainedIdentityHistoryReadRequest.AttributionPurpose,
                source.Length, DateTimeOffset.UtcNow, retained, excluded, "history-observation")
                { AuthorityRevision = "fixture-source-r1", ValidUntil = DateTimeOffset.UtcNow.AddMinutes(1) }, null));
        IOptionsMonitor<PartyIdentityOptions> options = Substitute.For<IOptionsMonitor<PartyIdentityOptions>>();
        options.CurrentValue.Returns(PolicyOptions());
        return (new(authority, TimeProvider.System, reader, custody, historyReader, options), authority, reader, custody, historyReader, options);
    }

    /// <summary>Suspended current reads cannot release a binding outside its completion interval or source observation.</summary>
    [Theory]
    [InlineData("interval-expiry")]
    [InlineData("custody-expiry")]
    [InlineData("source-rollback")]
    [InlineData("binding-rollback")]
    public async Task CurrentCompletionTimeChangesDuringCustody_DenyEvidence(string change)
    {
        HumanActorBindingEvidence binding = Binding() with { ValidUntil = Start.AddDays(5) };
        if (change == "custody-expiry")
        {
            binding = binding with { ValidUntil = binding.Custody.ExpiresAt };
        }

        var fixture = Service(Events()[0], new HumanActorBindingEstablished(new(binding, "logical", "digest"), Start, 0));
        DateTimeOffset now = Start.AddDays(2);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_ => now);
        var grant = new IdentityAdmissionEvidence(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"),
            "reader", null, Actor, 1, true, Start.AddDays(-1), Start.AddDays(20), 1);
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(new PartyIdentityAdmissionResult(grant, null));
        AuthoritativeStreamReadResult source = await fixture.Reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        DateTimeOffset observedAt = change == "binding-rollback" ? Start.AddHours(-1) : Start.AddDays(1);
        fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(source with { Stream = source.Stream! with { ObservedAt = observedAt } });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.SetResult(); return pending.Task; });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, identityOptions: fixture.Options);
        Task<PartyIdentityResult> result = service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        now = change switch
        {
            "interval-expiry" => binding.ValidUntil!.Value,
            "custody-expiry" => binding.Custody.ExpiresAt,
            "source-rollback" => observedAt.AddTicks(-1),
            _ => binding.ValidFrom.AddTicks(-1),
        };
        pending.SetResult(true);
        PartyIdentityResult completed = await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        completed.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        completed.Evidence.ShouldBeNull();
    }

    /// <summary>Authority withdrawn while either current dependency is suspended cannot release evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentAuthorityChangesDuringAwait_DenyEvidence(bool inCustody)
    {
        var fixture = Service(Events());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action complete;
        if (inCustody)
        {
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
                .Returns(_ => { entered.SetResult(); return pending.Task; });
            complete = () => pending.SetResult(true);
        }
        else
        {
            AuthoritativeStreamReadResult source = await fixture.Reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
            var pending = new TaskCompletionSource<AuthoritativeStreamReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(_ => { entered.SetResult(); return pending.Task; });
            complete = () => pending.SetResult(source);
        }

        Task result = AssertUnavailableAsync(fixture.Service, historical: false);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(new PartyIdentityAdmissionResult(null, "authority-withdrawn"));
        complete();
        await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CustodyExpiryCrossingAwait_DeniesHistoricalEvidence()
    {
        var fixture = Service(Events());
        DateTimeOffset now = Start.AddDays(9);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_ => now);
        var admitted = new IdentityAdmissionEvidence(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"),
            "reader", null, Actor, 1, false, Start, Start.AddDays(20), 1);
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(new PartyIdentityAdmissionResult(admitted, null));
        RetainedIdentityHistoryReadResult source = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"),
            RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
        fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(source with { Stream = source.Stream! with { ValidUntil = Binding().Custody.ExpiresAt } });
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => { now = Binding().Custody.ExpiresAt; return true; });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, custody: fixture.Custody, historyReader: fixture.HistoryReader, identityOptions: fixture.Options);
        HumanActorBindingResult result = await service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        result.Outcome.ShouldBe(HumanActorBindingOutcome.Expired);
        result.Evidence.ShouldBeNull();
        result.BindingSourcePosition.ShouldBe(0);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("different-actor")]
    [InlineData("different-revision")]
    [InlineData("different-source")]
    public async Task ReadAuthorityChangesDuringCustody_DenyWithoutSubstitutingActor(string change)
    {
        var fixture = Service(Events());
        IdentityAdmissionEvidence original = fixture.Authority.Admit(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1))).Evidence!;
        IdentityAdmissionEvidence? changed = change switch
        {
            "revoked" => null,
            "different-actor" => original with { TargetActorId = "01HX0000000000000000000002" },
            "different-revision" => original with { AuthorityRevision = original.AuthorityRevision + 1 },
            _ => original with { SourceId = "different-reader" },
        };
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(new PartyIdentityAdmissionResult(original, null),
            new PartyIdentityAdmissionResult(original, null), new PartyIdentityAdmissionResult(changed, changed is null ? "authority-revoked" : null));
        HumanActorBindingResult result = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        result.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        result.Evidence.ShouldBeNull();
        result.BindingSourcePosition.ShouldBe(0);
    }

    [Fact]
    public async Task RetainedCertificateExpiryCrossingCustodyAwait_DeniesWithoutRenewingObservation()
    {
        var fixture = Service(Events());
        RetainedIdentityHistoryReadResult source = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"),
            RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow.AddSeconds(1);
        DateTimeOffset deadline = now.AddSeconds(1);
        source = source with { Stream = source.Stream! with { ValidUntil = deadline } };
        fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(source);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_ => now);
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => { now = deadline; return true; });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, custody: fixture.Custody, historyReader: fixture.HistoryReader, identityOptions: fixture.Options);
        HumanActorBindingResult result = await service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        result.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        result.Evidence.ShouldBeNull();
        source.Stream!.ValidUntil.ShouldBe(deadline);
        await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }

    [Fact]
    public async Task ErasedProfile_CurrentReadFailsWhileIndependentHistoryKeepsOriginalPosition()
    {
        var fixture = Service([.. Events(), new PartyErased { TenantId = "tenant-a", PartyId = "party-1", ErasedAt = Start.AddHours(1) }]);
        fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(new AuthoritativeStreamReadResult(null, "profile-key-destroyed"));
        PartyIdentityResult current = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
        current.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        HumanActorBindingResult historical = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        historical.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        historical.SourcePosition.ShouldBe(3);
        historical.BindingSourcePosition.ShouldBe(2);
        historical.Evidence!.ActorId.ShouldBe(Actor);
        JsonSerializer.Serialize(historical, PartiesJsonOptions.Default).ShouldNotContain("Private");
        await fixture.Reader.Received(1).ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SerializedRetainedSource_ReconstructsBothIntervalsWithoutProfileOrCurrentBinding()
    {
        DateTimeOffset boundary = Start.AddHours(1);
        string successor = "01HX0000000000000000000002";
        HumanActorBindingEvidence next = Binding() with { ActorId = successor, BindingVersion = 2, ValidFrom = boundary, ValidUntil = boundary.AddDays(10),
            Custody = Binding().Custody with { ExpiresAt = boundary.AddDays(10) } };
        var fixture = Service([.. Events(), new HumanActorBindingRebound(new(next, "rebind", "digest-two"), boundary, 1),
            new PartyErased { TenantId = "tenant-a", PartyId = "party-1", ErasedAt = boundary }]);
        RetainedIdentityHistoryReadResult source = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"),
            RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
        // This exercises the stored wire representation and a fresh domain service, not live storage/restore qualification.
        byte[] stored = JsonSerializer.SerializeToUtf8Bytes(source, PartiesJsonOptions.Default);
        stored.Length.ShouldBeGreaterThan(0);
        string serialized = System.Text.Encoding.UTF8.GetString(stored);
        serialized.ShouldNotContain("Private");
        serialized.ShouldNotContain(nameof(PartyCreated));
        RetainedIdentityHistoryReadResult restored = JsonSerializer.Deserialize<RetainedIdentityHistoryReadResult>(stored, PartiesJsonOptions.Default)!;
        IRetainedIdentityHistoryReader restoredReader = Substitute.For<IRetainedIdentityHistoryReader>();
        restoredReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(restored);
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(call =>
        {
            string actor = JsonSerializer.Deserialize<ResolveHumanActorBindingAt>(call.Arg<QueryEnvelope>().Payload, PartiesJsonOptions.Default)!.ExpectedActorId;
            return new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"), "reader", null, actor,
                3, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), null);
        });
        var restarted = new PartyIdentityQueryService(fixture.Authority, TimeProvider.System, custody: fixture.Custody, historyReader: restoredReader, identityOptions: fixture.Options);
        HumanActorBindingResult before = await restarted.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary.AddTicks(-1), Actor, 1)), TestContext.Current.CancellationToken);
        HumanActorBindingResult at = await restarted.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary, successor, 2)), TestContext.Current.CancellationToken);
        before.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        at.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        before.Evidence!.ValidUntil.ShouldBe(boundary);
        before.BindingSourcePosition.ShouldBe(2);
        at.BindingSourcePosition.ShouldBe(3);
        at.SourcePosition.ShouldBe(4);
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("overlap")]
    [InlineData("profile-substitution")]
    [InlineData("wrong-purpose")]
    [InlineData("foreign-binding")]
    [InlineData("transit-expiry")]
    [InlineData("missing-authority")]
    [InlineData("future-observation")]
    [InlineData("short-name-substitution")]
    [InlineData("assembly-qualified-substitution")]
    [InlineData("namespace-alias-substitution")]
    public async Task RetainedCertificateOrEventSubstitution_DeniesWithoutProfileFallback(string corruption)
    {
        var fixture = Service(Events());
        RetainedIdentityHistoryReadResult original = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"),
            RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
        RetainedIdentityHistoryStream stream = original.Stream!;
        stream = corruption switch
        {
            "gap" => stream with { ExcludedSequences = [] },
            "overlap" => stream with { ExcludedSequences = [2] },
            "wrong-purpose" => stream with { Purpose = "party-profile" },
            "transit-expiry" => stream with { ValidUntil = DateTimeOffset.UtcNow.AddTicks(-1) },
            "missing-authority" => stream with { AuthorityRevision = null },
            "future-observation" => stream with { ObservedAt = DateTimeOffset.UtcNow.AddMinutes(1) },
            "short-name-substitution" => stream with { Events = [stream.Events[0] with { EventTypeName = nameof(HumanActorBindingEstablished) }] },
            "assembly-qualified-substitution" => stream with { Events = [stream.Events[0] with { EventTypeName = typeof(HumanActorBindingEstablished).AssemblyQualifiedName! }] },
            "namespace-alias-substitution" => stream with { Events = [stream.Events[0] with { EventTypeName = "Legacy.Parties.Contracts.Events.HumanActorBindingEstablished" }] },
            "profile-substitution" => stream with { Events = [stream.Events[0] with
                { EventTypeName = typeof(PartyCreated).FullName!, Payload = JsonSerializer.SerializeToUtf8Bytes(Events()[0], PartiesJsonOptions.Default) }] },
            _ => stream with { Events = [stream.Events[0] with { Payload = JsonSerializer.SerializeToUtf8Bytes(
                new HumanActorBindingEstablished(new(Binding() with { TenantId = "tenant-b" }, "logical", "digest"), Start, 0), PartiesJsonOptions.Default) }] },
        };
        fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RetainedIdentityHistoryReadResult(stream, null));
        HumanActorBindingResult result = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        result.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        result.Evidence.ShouldBeNull();
        result.BindingSourcePosition.ShouldBe(0);
        await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    [Fact]
    public async Task MissingRetainedReaderOrExpiredCustody_CannotFallBackToAvailableFullProfile()
    {
        var fixture = Service(Events());
        var missing = new PartyIdentityQueryService(fixture.Authority, TimeProvider.System, fixture.Reader, fixture.Custody, identityOptions: fixture.Options);
        var query = new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1);
        (await missing.ResolveAtAsync(Envelope(query), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>()).Returns(false);
        HumanActorBindingResult expired = await fixture.Service.ResolveAtAsync(Envelope(query), TestContext.Current.CancellationToken);
        expired.Outcome.ShouldBe(HumanActorBindingOutcome.Expired);
        expired.Evidence.ShouldBeNull();
        await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }

    [Fact]
    public async Task ProvisionedOrganization_KeepsBranchBClassificationAndNeverCarriesHumanBinding()
    {
        var intent = new AgentPartyIdentity("tenant-a", Actor, "party-1", 1, "provision", new string('a', 64), Start);
        var fixture = Service(new PartyCreated { Type = PartyType.Organization, CreatedAt = Start,
                OrganizationDetails = new OrganizationDetails { LegalName = "Agent" } },
            new AgentPartyProvisioned(new(intent, 2, "provisioner")));
        fixture.Options.CurrentValue.Returns(new PartyIdentityOptions());
        PartyIdentityResult result = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1")), TestContext.Current.CancellationToken);
        result.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
        result.Evidence!.Classification.ShouldBe(PartyIdentityClassification.Organization);
        result.Evidence.HumanBinding.ShouldBeNull();
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InactiveOrRestrictedHuman_ReturnsIneligibleWithoutUsableBinding(bool inactive)
    {
        IEventPayload transition = inactive ? new PartyDeactivated()
            : new ProcessingRestricted { TenantId = "tenant-a", PartyId = "party-1", RestrictedAt = Start };
        var fixture = Service([.. Events(), transition]);
        PartyIdentityResult result = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
        result.Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);
        result.Evidence!.HumanBinding.ShouldBeNull();
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    [Fact]
    public async Task CurrentHuman_RequiresCompleteSourceAndCurrentExactActor()
    {
        var fixture = Service(Events());
        PartyIdentityResult result = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
        result.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
        result.Evidence!.Classification.ShouldBe(PartyIdentityClassification.Human);
        result.Evidence.HumanBinding!.ActorId.ShouldBe(Actor);
        JsonSerializer.Serialize(result, PartiesJsonOptions.Default).ShouldNotContain("Private");
        (await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", "01HX0000000000000000000002")), TestContext.Current.CancellationToken)).Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);
    }

    [Fact]
    public async Task HistoricalRead_DoesNotRequireCurrentActorActivityAndHonorsHalfOpenBoundary()
    {
        var fixture = Service(Events());
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"), "reader", null, Actor, 2, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), null));
        var query = new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1);
        (await fixture.Service.ResolveAtAsync(Envelope(query), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        (await fixture.Service.ResolveAtAsync(Envelope(query with { ActionAt = Start.AddTicks(-1) }), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Gap);
        (await fixture.Service.ResolveAtAsync(Envelope(query with { ExpectedBindingVersion = 2 }), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Mismatch);
    }

    [Fact]
    public async Task HistoricalReplay_BeforeBoundaryReturnsOriginalAndAtBoundaryReturnsSuccessor()
    {
        DateTimeOffset boundary = Start.AddHours(1);
        string successor = "01HX0000000000000000000002";
        HumanActorBindingEvidence second = Binding() with { ActorId = successor, BindingVersion = 2, ValidFrom = boundary, ValidUntil = boundary.AddDays(10),
            Custody = Binding().Custody with { ExpiresAt = boundary.AddDays(10) } };
        var fixture = Service(Events().Append(new HumanActorBindingRebound(new(second, "rebind", "digest-two"), boundary, 1)).ToArray());
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(call =>
        {
            string target = JsonSerializer.Deserialize<ResolveHumanActorBindingAt>(call.Arg<QueryEnvelope>().Payload, PartiesJsonOptions.Default)!.ExpectedActorId;
            return new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"), "reader", null, target, 3, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), null);
        });
        HumanActorBindingResult before = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary.AddTicks(-1), Actor, 1)), TestContext.Current.CancellationToken);
        HumanActorBindingResult at = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary, successor, 2)), TestContext.Current.CancellationToken);
        before.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        at.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        before.Evidence!.ActorId.ShouldBe(Actor);
        at.Evidence!.ActorId.ShouldBe(successor);
        before.Evidence.ValidUntil.ShouldBe(boundary);
    }

    [Fact]
    public async Task ForeignAuthoritativeSourceAndFutureAction_DenyWithNoEvidence()
    {
        var fixture = Service(new PartyCreated { Type = PartyType.Organization, OrganizationDetails = new OrganizationDetails { LegalName = "Foreign" } });
        fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(new AuthoritativeStreamReadResult(new(new("tenant-b", "party", "foreign-party"), 1, DateTimeOffset.UtcNow,
                [new(1, typeof(PartyCreated).FullName!, JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Organization }, PartiesJsonOptions.Default), "json", 1, "m", null, null, Start, "writer")], "observation"), null));
        PartyIdentityResult current = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1")), TestContext.Current.CancellationToken);
        current.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        current.Evidence.ShouldBeNull();
        fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RetainedIdentityHistoryReadResult(new(new("tenant-b", "party", "foreign-party"), RetainedIdentityHistoryReadRequest.AttributionPurpose,
                0, DateTimeOffset.UtcNow, [], [], "history-observation"), null));
        HumanActorBindingResult history = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        history.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        history.Evidence.ShouldBeNull();
        var futureFixture = Service(Events());
        HumanActorBindingResult future = await futureFixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", DateTimeOffset.UtcNow.AddDays(1), Actor, 1)), TestContext.Current.CancellationToken);
        future.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        future.Evidence.ShouldBeNull();
    }

    [Fact]
    public async Task ValidRevocationReplay_PreservesBeforeBoundaryAndReturnsGapAtBoundary()
    {
        DateTimeOffset boundary = Start.AddHours(1);
        var fixture = Service([.. Events(), new HumanActorBindingRevoked("revoke", "digest-two", 1, boundary, Binding().Custody)]);
        HumanActorBindingResult before = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary.AddTicks(-1), Actor, 1)), TestContext.Current.CancellationToken);
        before.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        before.Evidence!.ActorId.ShouldBe(Actor);
        before.Evidence.BindingVersion.ShouldBe(1);
        before.Evidence.ValidUntil.ShouldBe(boundary);
        HumanActorBindingResult at = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary, Actor, 1)), TestContext.Current.CancellationToken);
        at.Outcome.ShouldBe(HumanActorBindingOutcome.Gap);
        at.Evidence.ShouldBeNull();
    }

    [Fact]
    public async Task WrongAuthorizedActor_DeniesBeforeSourceRead()
    {
        var fixture = Service(Events());
        var query = new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, "01HX0000000000000000000002", 1);
        (await fixture.Service.ResolveAtAsync(Envelope(query), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RevocationWithoutLivePredecessor_DeniesWithNoEvidence(bool missingPredecessor)
    {
        IEventPayload[] events = missingPredecessor ? [Events()[0]] : Events();
        var fixture = Service([.. events, new HumanActorBindingRevoked("revoke", "digest-two", missingPredecessor ? 0 : 1, Start, Binding().Custody)]);
        PartyIdentityResult current = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
        current.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        current.Evidence.ShouldBeNull();
        HumanActorBindingResult history = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        history.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        history.Evidence.ShouldBeNull();
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NonFiniteOrBeyondCustodyHistory_DeniesWithNoEvidence(bool missingEnd)
    {
        HumanActorBindingEvidence malformed = Binding() with
        {
            ValidUntil = missingEnd ? null : Binding().Custody.ExpiresAt.AddTicks(1),
        };
        var fixture = Service(Events()[0], new HumanActorBindingEstablished(new(malformed, "logical", "digest"), Start, 0));
        PartyIdentityResult current = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
        current.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        current.Evidence.ShouldBeNull();
        HumanActorBindingResult history = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        history.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        history.Evidence.ShouldBeNull();
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    [Fact]
    public async Task CustodyFailureOrNetworkFailure_IsUnavailableAndCancellationPropagates()
    {
        var fixture = Service(Events());
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new HttpRequestException("unavailable")));
        var query = new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1);
        (await fixture.Service.ResolveAtAsync(Envelope(query), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromCanceled<RetainedIdentityHistoryReadResult>(call.Arg<CancellationToken>()));
        await Should.ThrowAsync<OperationCanceledException>(() => fixture.Service.ResolveAtAsync(Envelope(query), canceled.Token));
    }

    [Fact]
    public async Task UncreatedInactiveRestrictedUnknownAndErased_AreNeverResolved()
    {
        foreach (IEventPayload[] events in new[] { Array.Empty<IEventPayload>(), [new PartyCreated { Type = PartyType.Unknown }],
            Events().Append(new PartyDeactivated()).ToArray(), Events().Append(new ProcessingRestricted { TenantId = "tenant-a", PartyId = "party-1", RestrictedAt = Start }).ToArray(),
            Events().Append(new PartyErased { TenantId = "tenant-a", PartyId = "party-1", ErasedAt = Start }).ToArray() })
        {
            (await Service(events).Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken)).Outcome.ShouldNotBe(PartyIdentityOutcome.Resolved);
        }
    }

    /// <summary>Verifies pre cancelled query  does not consult authority or readers.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCancelledQuery_DoesNotConsultAuthorityOrReaders(bool historical)
    {
        var fixture = Service(Events());
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => InvokeIdentityAsync(fixture.Service, historical, caller.Token));
        fixture.Authority.DidNotReceiveWithAnyArgs().Admit(default(QueryEnvelope)!);
        await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
        await fixture.HistoryReader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default);
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    /// <summary>Verifies reader cancels then returns valid source  does not release identity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReaderCancelsThenReturnsValidSource_DoesNotReleaseIdentity(bool historical)
    {
        var fixture = Service(Events());
        using var caller = new CancellationTokenSource();
        if (historical)
        {
            RetainedIdentityHistoryReadResult valid = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"), RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
            fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(_ => { caller.Cancel(); return Task.FromResult(valid); });
        }
        else
        {
            AuthoritativeStreamReadResult valid = await fixture.Reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
            fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(_ => { caller.Cancel(); return Task.FromResult(valid); });
        }

        await Should.ThrowAsync<OperationCanceledException>(() => InvokeIdentityAsync(fixture.Service, historical, caller.Token));
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    /// <summary>Verifies non cooperative reader or custody  caller can cancel outstanding read.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task NonCooperativeReaderOrCustody_CallerCanCancelOutstandingRead(bool historical, bool inCustody)
    {
        var fixture = Service(Events());
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (inCustody)
        {
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
                .Returns(_ => { entered.TrySetResult(); return pending.Task; });
        }
        else if (historical)
        {
            var pending = new TaskCompletionSource<RetainedIdentityHistoryReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(_ => { entered.TrySetResult(); return pending.Task; });
        }
        else
        {
            var pending = new TaskCompletionSource<AuthoritativeStreamReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(_ => { entered.TrySetResult(); return pending.Task; });
        }

        Task result = InvokeIdentityAsync(fixture.Service, historical, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await caller.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies custody cancels then returns  does not consult final authority or release identity.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CustodyCancelsThenReturns_DoesNotConsultFinalAuthorityOrReleaseIdentity(bool historical, bool canRead)
    {
        var fixture = Service(Events());
        using var caller = new CancellationTokenSource();
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => { caller.Cancel(); return Task.FromResult(canRead); });
        await Should.ThrowAsync<OperationCanceledException>(() => InvokeIdentityAsync(fixture.Service, historical, caller.Token));
        fixture.Authority.Received(historical ? 2 : 1).Admit(Arg.Any<QueryEnvelope>());
    }

    /// <summary>Verifies final authority cancels then returns valid grant  does not release identity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalAuthorityCancelsThenReturnsValidGrant_DoesNotReleaseIdentity(bool historical)
    {
        var fixture = Service(Events());
        using var caller = new CancellationTokenSource();
        PartyIdentityAdmissionResult grant = fixture.Authority.Admit(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)));
        int calls = 0;
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(_ =>
        {
            if (++calls == (historical ? 3 : 2))
            {
                caller.Cancel();
            }

            return grant;
        });
        await Should.ThrowAsync<OperationCanceledException>(() => InvokeIdentityAsync(fixture.Service, historical, caller.Token));
    }

    /// <summary>Missing or unsupported policy cannot release identity even when custody reports readable.</summary>
    [Theory]
    [InlineData("unregistered")]
    [InlineData("missing-id")]
    [InlineData("blank-id")]
    [InlineData("missing-duration")]
    [InlineData("zero-duration")]
    [InlineData("missing-trigger")]
    [InlineData("unsupported-trigger")]
    public async Task UnconfiguredPolicy_DeniesBothBindingReads(string missing)
    {
        var fixture = Service(Events());
        PartyIdentityOptions policy = PolicyOptions();
        switch (missing)
        {
            case "missing-id": policy.PolicyId = null; break;
            case "blank-id": policy.PolicyId = " "; break;
            case "missing-duration": policy.Retention = null; break;
            case "zero-duration": policy.Retention = TimeSpan.Zero; break;
            case "missing-trigger": policy.ExpiryTrigger = null; break;
            case "unsupported-trigger": policy.ExpiryTrigger = "party-erased-at"; break;
        }

        fixture.Options.CurrentValue.Returns(policy);
        var service = new PartyIdentityQueryService(fixture.Authority, TimeProvider.System, fixture.Reader,
            fixture.Custody, fixture.HistoryReader, missing == "unregistered" ? null : fixture.Options);
        await AssertUnavailableAsync(service, historical: true);
        await fixture.HistoryReader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default);
        await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
        await AssertUnavailableAsync(service, historical: false);
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    /// <summary>An accepted custody response does not substitute for exact policy and expiry matching.</summary>
    [Theory]
    [InlineData("policy-version")]
    [InlineData("duration")]
    [InlineData("recorded-expiry")]
    [InlineData("purpose")]
    [InlineData("source-expiry")]
    [InlineData("restore")]
    [InlineData("derived-copies")]
    public async Task MismatchedPolicy_DeniesBothBindingReadsWithoutCustody(string mismatch)
    {
        IdentityHistoryCustodyEvidence custody = Binding().Custody;
        custody = mismatch switch
        {
            "recorded-expiry" => custody with { ExpiresAt = custody.ExpiresAt.AddSeconds(1) },
            "purpose" => custody with { Purpose = "party-profile" },
            "source-expiry" => custody with { SourceExpiryEnforced = false },
            "restore" => custody with { RestoreSafe = false },
            "derived-copies" => custody with { DerivedCopiesCovered = false },
            _ => custody,
        };
        var fixture = Service(Events()[0], new HumanActorBindingEstablished(
            new(Binding() with { Custody = custody }, "logical", "digest"), Start, 0));
        PartyIdentityOptions policy = PolicyOptions();
        if (mismatch == "policy-version")
        {
            policy.PolicyId = "synthetic-v2";
        }
        else if (mismatch == "duration")
        {
            policy.Retention = TimeSpan.FromDays(11);
        }

        fixture.Options.CurrentValue.Returns(policy);
        await AssertUnavailableAsync(fixture.Service, historical: true);
        await AssertUnavailableAsync(fixture.Service, historical: false);
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    /// <summary>Actual suspended reads cannot release identity after policy withdrawal or a changed duration.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task PolicyChangesDuringAwait_DenyWithoutRelabelingExpiry(bool historical, bool inCustody, bool remove)
    {
        var fixture = Service(Events());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action complete;
        if (inCustody)
        {
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
                .Returns(_ => { entered.TrySetResult(); return pending.Task; });
            complete = () => pending.SetResult(true);
        }
        else if (historical)
        {
            RetainedIdentityHistoryReadResult valid = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"),
                RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
            var pending = new TaskCompletionSource<RetainedIdentityHistoryReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(_ => { entered.TrySetResult(); return pending.Task; });
            complete = () => pending.SetResult(valid);
        }
        else
        {
            AuthoritativeStreamReadResult valid = await fixture.Reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
            var pending = new TaskCompletionSource<AuthoritativeStreamReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(_ => { entered.TrySetResult(); return pending.Task; });
            complete = () => pending.SetResult(valid);
        }

        Task result = AssertUnavailableAsync(fixture.Service, historical);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        WithdrawOrChangePolicy(fixture.Options, remove);
        complete();
        await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Binding().Custody.ExpiresAt.ShouldBe(Start.AddDays(10));
        if (!inCustody)
        {
            await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
        }
    }

    /// <summary>The final authority check cannot withdraw policy and still release an otherwise valid binding.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PolicyChangesAtFinalAuthority_DenyBothReads(bool historical, bool remove)
    {
        var fixture = Service(Events());
        PartyIdentityAdmissionResult grant = fixture.Authority.Admit(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)));
        int calls = 0;
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(_ =>
        {
            if (++calls == (historical ? 3 : 2))
            {
                WithdrawOrChangePolicy(fixture.Options, remove);
            }

            return grant;
        });
        await AssertUnavailableAsync(fixture.Service, historical);
    }

    private static void WithdrawOrChangePolicy(IOptionsMonitor<PartyIdentityOptions> options, bool remove)
    {
        PartyIdentityOptions replacement = remove ? new PartyIdentityOptions() : PolicyOptions();
        if (!remove)
        {
            replacement.Retention = TimeSpan.FromDays(11);
        }

        options.CurrentValue.Returns(replacement);
    }

    private static async Task AssertUnavailableAsync(PartyIdentityQueryService service, bool historical)
    {
        if (historical)
        {
            HumanActorBindingResult result = await service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken).ConfigureAwait(false);
            result.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
            result.Evidence.ShouldBeNull();
            result.BindingSourcePosition.ShouldBe(0);
        }
        else
        {
            PartyIdentityResult result = await service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken).ConfigureAwait(false);
            result.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
            result.Evidence.ShouldBeNull();
        }
    }

    private static PartyIdentityOptions PolicyOptions() => new()
    {
        PolicyId = "synthetic",
        Retention = TimeSpan.FromDays(10),
        ExpiryTrigger = "binding-effective-at",
    };

    private static Task InvokeIdentityAsync(PartyIdentityQueryService service, bool historical, CancellationToken token)
        => historical
            ? service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), token)
            : service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), token);
}
