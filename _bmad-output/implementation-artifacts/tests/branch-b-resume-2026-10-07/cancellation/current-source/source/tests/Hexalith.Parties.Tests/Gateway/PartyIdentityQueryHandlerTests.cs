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
        ConfigureSystemTimer(clock);
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
        ConfigureSystemTimer(clock);
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
        ConfigureSystemTimer(clock);
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
        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => InvokeIdentityAsync(fixture.Service, historical, caller.Token));
        exception.CancellationToken.ShouldBe(caller.Token);
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
        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        exception.CancellationToken.ShouldBe(caller.Token);
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
            result.SourcePosition.ShouldBe(0);
            result.ObservationId.ShouldBeNull();
            result.TenantId.ShouldBe("tenant-a");
            result.PartyId.ShouldBe("party-1");
            result.ActionAt.ShouldBe(Start);
        }
        else
        {
            PartyIdentityResult result = await service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken).ConfigureAwait(false);
            result.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
            result.Evidence.ShouldBeNull();
        }
    }

    /// <summary>Accepted 365-day intervals retain their exact boundary and source positions through serialized restore.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted365DayPolicy_RebindAndRestoredHistoryPreserveOriginalBoundary(bool restored)
    {
        const string successorActor = "01HX0000000000000000000002";
        DateTimeOffset boundary = Start.AddDays(180);
        HumanActorBindingEvidence first = Binding() with
        {
            ValidUntil = Start.AddDays(365),
            Custody = Binding().Custody with { PolicyId = "party-actor-retention-v1", ExpiresAt = Start.AddDays(365) },
        };
        HumanActorBindingEvidence second = first with
        {
            ActorId = successorActor, BindingVersion = 2, ValidFrom = boundary, ValidUntil = boundary.AddDays(365),
            Custody = first.Custody with { ExpiresAt = boundary.AddDays(365), LifecycleRevision = 2, EvidenceId = "successor-custody" },
        };
        var fixture = Service(Events()[0], new HumanActorBindingEstablished(new(first, "first", "first-digest"), Start, 0),
            new HumanActorBindingRebound(new(second, "second", "second-digest"), boundary, 1));
        DateTimeOffset now = Start.AddDays(200);
        var policy = new PartyIdentityOptions { PolicyId = "party-actor-retention-v1", Retention = TimeSpan.FromDays(365), ExpiryTrigger = "binding-effective-at" };
        fixture.Options.CurrentValue.Returns(policy);
        TimeProvider clock = Substitute.For<TimeProvider>();
        ConfigureSystemTimer(clock);
        clock.GetUtcNow().Returns(now);
        RetainedIdentityHistoryReadResult captured = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"),
            RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
        RetainedIdentityHistoryStream source = captured.Stream! with { ObservedAt = now, ValidUntil = now.AddMinutes(1) };
        if (restored)
        {
            source = JsonSerializer.Deserialize<RetainedIdentityHistoryStream>(JsonSerializer.SerializeToUtf8Bytes(source, PartiesJsonOptions.Default), PartiesJsonOptions.Default)!;
        }
        fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RetainedIdentityHistoryReadResult(source, null));
        string requestedActor = Actor;
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(_ => new PartyIdentityAdmissionResult(
            new(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"), "reader", null, requestedActor, 1, true,
                Start, Start.AddDays(600), 1), null));
        var service = new PartyIdentityQueryService(fixture.Authority, clock, custody: fixture.Custody, historyReader: fixture.HistoryReader, identityOptions: fixture.Options);
        HumanActorBindingResult before = await service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary.AddTicks(-1), Actor, 1)), TestContext.Current.CancellationToken);
        before.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        before.BindingSourcePosition.ShouldBe(2);
        before.Evidence!.ActorId.ShouldBe(Actor);
        before.Evidence.ValidUntil.ShouldBe(boundary);
        before.Evidence.Custody.ExpiresAt.ShouldBe(Start.AddDays(365));
        requestedActor = successorActor;
        HumanActorBindingResult at = await service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary, successorActor, 2)), TestContext.Current.CancellationToken);
        at.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        at.BindingSourcePosition.ShouldBe(3);
        at.Evidence.ShouldBe(second);
        await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }

    /// <summary>Restored readable bytes and optimistic custody cannot restart the accepted immutable expiry clock.</summary>
    [Theory]
    [InlineData(-1, HumanActorBindingOutcome.Resolved)]
    [InlineData(0, HumanActorBindingOutcome.Expired)]
    [InlineData(1, HumanActorBindingOutcome.Expired)]
    public async Task Accepted365DayPolicy_RestoredBytesRespectExclusiveExpiry(int ticks, HumanActorBindingOutcome expected)
    {
        HumanActorBindingEvidence binding = Binding() with
        {
            ValidUntil = Start.AddDays(365),
            Custody = Binding().Custody with { PolicyId = "party-actor-retention-v1", ExpiresAt = Start.AddDays(365) },
        };
        var fixture = Service(Events()[0], new HumanActorBindingEstablished(new(binding, "logical", "digest"), Start, 0));
        fixture.Options.CurrentValue.Returns(new PartyIdentityOptions { PolicyId = "party-actor-retention-v1", Retention = TimeSpan.FromDays(365), ExpiryTrigger = "binding-effective-at" });
        DateTimeOffset now = binding.Custody.ExpiresAt.AddTicks(ticks);
        TimeProvider clock = Substitute.For<TimeProvider>();
        ConfigureSystemTimer(clock);
        clock.GetUtcNow().Returns(now);
        RetainedIdentityHistoryReadResult captured = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"), RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
        RetainedIdentityHistoryStream restored = JsonSerializer.Deserialize<RetainedIdentityHistoryStream>(
            JsonSerializer.SerializeToUtf8Bytes(captured.Stream! with { ObservedAt = now, ValidUntil = now.AddMinutes(1) }, PartiesJsonOptions.Default), PartiesJsonOptions.Default)!;
        fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new RetainedIdentityHistoryReadResult(restored, null));
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"), "reader", null, Actor, 1, true, Start, Start.AddDays(600), 1), null));
        var service = new PartyIdentityQueryService(fixture.Authority, clock, custody: fixture.Custody, historyReader: fixture.HistoryReader, identityOptions: fixture.Options);

        HumanActorBindingResult result = await service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(expected);
        if (expected == HumanActorBindingOutcome.Expired)
        {
            result.Evidence.ShouldBeNull();
            result.BindingSourcePosition.ShouldBe(0);
            await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
        }
        restored.Events[0].Payload.ShouldBe(captured.Stream!.Events[0].Payload);
    }

    /// <summary>A profile exclusion cannot supply missing version evidence for a successor after predecessor destruction.</summary>
    [Fact]
    public async Task ExpiredPredecessorOmitted_SuccessorReadRemainsUnavailable()
    {
        const string successorActor = "01HX0000000000000000000002";
        DateTimeOffset boundary = Start.AddDays(180);
        HumanActorBindingEvidence successor = Binding() with
        {
            ActorId = successorActor, BindingVersion = 2, ValidFrom = boundary, ValidUntil = boundary.AddDays(365),
            Custody = Binding().Custody with { PolicyId = "party-actor-retention-v1", ExpiresAt = boundary.AddDays(365), LifecycleRevision = 2 },
        };
        var fixture = Service(Events()[0], new HumanActorBindingEstablished(new(Binding(), "first", "digest"), Start, 0),
            new HumanActorBindingRebound(new(successor, "second", "digest-2"), boundary, 1));
        fixture.Options.CurrentValue.Returns(new PartyIdentityOptions { PolicyId = "party-actor-retention-v1", Retention = TimeSpan.FromDays(365), ExpiryTrigger = "binding-effective-at" });
        DateTimeOffset now = Start.AddDays(366);
        TimeProvider clock = Substitute.For<TimeProvider>();
        ConfigureSystemTimer(clock);
        clock.GetUtcNow().Returns(now);
        RetainedIdentityHistoryReadResult captured = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"), RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
        RetainedIdentityHistoryStream missing = captured.Stream! with { ObservedAt = now, ValidUntil = now.AddMinutes(1), Events = [captured.Stream!.Events[1]], ExcludedSequences = [1, 2] };
        RetainedIdentityHistoryValidator.IsComplete(new(missing.Identity, missing.Purpose), missing, now).ShouldBeTrue();
        fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new RetainedIdentityHistoryReadResult(missing, null));
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"), "reader", null, successorActor, 1, true, Start, Start.AddDays(600), 1), null));
        var service = new PartyIdentityQueryService(fixture.Authority, clock, custody: fixture.Custody, historyReader: fixture.HistoryReader, identityOptions: fixture.Options);

        HumanActorBindingResult result = await service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary, successorActor, 2)), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        result.Evidence.ShouldBeNull();
        result.BindingSourcePosition.ShouldBe(0);
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    /// <summary>One deadline cancels noncooperative source/custody and cannot admit a late success or fault.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task WholeQueryDeadline_NonCooperativeDependencyDeniesLateEvidence(bool historical, bool inCustody, bool lateFault)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken providerToken = default;
        Action complete;
        async Task<T> FinishProviderAsync<T>(Task<T> pending, CancellationToken token)
        {
            providerToken = token;
            entered.TrySetResult();
            try { return await pending.ConfigureAwait(false); }
            finally { providerReturned.TrySetResult(); }
        }

        if (inCustody)
        {
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
                .Returns(call => FinishProviderAsync(pending.Task, call.Arg<CancellationToken>()));
            complete = () => { if (lateFault) { pending.SetException(new HttpRequestException("late synthetic fault")); } else { pending.SetResult(true); } };
        }
        else if (historical)
        {
            RetainedIdentityHistoryReadResult valid = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"), RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
            var pending = new TaskCompletionSource<RetainedIdentityHistoryReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => FinishProviderAsync(pending.Task, call.Arg<CancellationToken>()));
            complete = () => { if (lateFault) { pending.SetException(new HttpRequestException("late synthetic fault")); } else { pending.SetResult(valid); } };
        }
        else
        {
            AuthoritativeStreamReadResult valid = await fixture.Reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
            var pending = new TaskCompletionSource<AuthoritativeStreamReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(call => FinishProviderAsync(pending.Task, call.Arg<CancellationToken>()));
            complete = () => { if (lateFault) { pending.SetException(new HttpRequestException("late synthetic fault")); } else { pending.SetResult(valid); } };
        }

        Task result = AssertUnavailableAsync(service, historical);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        result.IsCompleted.ShouldBeFalse();
        advance(TimeSpan.FromSeconds(30), true);
        await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        providerToken.IsCancellationRequested.ShouldBeTrue();
        int authorityCalls = fixture.Authority.ReceivedCalls().Count();
        providerReturned.Task.IsCompleted.ShouldBeFalse();
        complete();
        await providerReturned.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        fixture.Authority.ReceivedCalls().Count().ShouldBe(authorityCalls);
        if (!inCustody)
        {
            await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
        }
    }

    /// <summary>The budget covers providers blocked synchronously before returning their task.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task WholeQueryDeadline_SynchronousProviderInvocationIsBounded(bool historical, bool inCustody)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken providerToken = default;
        void Block(CancellationToken token)
        {
            providerToken = token;
            entered.TrySetResult();
            release.Wait(TestContext.Current.CancellationToken);
            returned.TrySetResult();
        }

        if (inCustody)
        {
            fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
                .Returns(call => { Block(call.Arg<CancellationToken>()); return Task.FromResult(true); });
        }
        else if (historical)
        {
            RetainedIdentityHistoryReadResult valid = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"), RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
            fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => { Block(call.Arg<CancellationToken>()); return Task.FromResult(valid); });
        }
        else
        {
            AuthoritativeStreamReadResult valid = await fixture.Reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
            fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(call => { Block(call.Arg<CancellationToken>()); return Task.FromResult(valid); });
        }

        Task result = Task.Run(() => AssertUnavailableAsync(service, historical), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            advance(TimeSpan.FromSeconds(30), true);
            await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            providerToken.IsCancellationRequested.ShouldBeTrue();
        }
        finally
        {
            release.Set();
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Source and custody consume one budget and receive the same cancellable operation token.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WholeQueryDeadline_SourceAndCustodyShareCumulativeBudget(bool historical)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        PartyIdentityOptions configured = PolicyOptions();
        configured.QueryTimeout = TimeSpan.FromSeconds(10);
        fixture.Options.CurrentValue.Returns(configured);
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        var sourceEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var custodyEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var custodyPending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken sourceToken = default;
        CancellationToken custodyToken = default;
        Action releaseSource;
        if (historical)
        {
            RetainedIdentityHistoryReadResult valid = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"), RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
            var source = new TaskCompletionSource<RetainedIdentityHistoryReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => { sourceToken = call.Arg<CancellationToken>(); sourceEntered.SetResult(); return source.Task; });
            releaseSource = () => source.SetResult(valid);
        }
        else
        {
            AuthoritativeStreamReadResult valid = await fixture.Reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
            var source = new TaskCompletionSource<AuthoritativeStreamReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(call => { sourceToken = call.Arg<CancellationToken>(); sourceEntered.SetResult(); return source.Task; });
            releaseSource = () => source.SetResult(valid);
        }

        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(call => { custodyToken = call.Arg<CancellationToken>(); custodyEntered.SetResult(); return custodyPending.Task; });
        Task result = AssertUnavailableAsync(service, historical);
        await sourceEntered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        advance(TimeSpan.FromSeconds(7), true);
        result.IsCompleted.ShouldBeFalse();
        releaseSource();
        await custodyEntered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        sourceToken.ShouldBe(custodyToken);
        advance(TimeSpan.FromSeconds(3), true);
        await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        custodyToken.IsCancellationRequested.ShouldBeTrue();
        custodyPending.SetResult(true);
        clock.Received(1).CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
    }

    /// <summary>Monotonic elapsed checks deny late evidence even if a timer callback is delayed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WholeQueryDeadline_ElapsedBeforeTimerCallbackDeniesEvidence(bool historical)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => { advance(TimeSpan.FromSeconds(30), false); return true; });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        await AssertUnavailableAsync(service, historical);
        fixture.Authority.Received(historical ? 2 : 1).Admit(Arg.Any<QueryEnvelope>());
    }

    /// <summary>Invalid operational bounds fail closed before authority, source or custody invocation.</summary>
    [Theory]
    [InlineData(false, 0L)]
    [InlineData(true, 0L)]
    [InlineData(false, -1L)]
    [InlineData(true, -1L)]
    [InlineData(false, 300_000_001L)]
    [InlineData(true, 300_000_001L)]
    [InlineData(false, long.MaxValue)]
    [InlineData(true, long.MaxValue)]
    public async Task InvalidQueryTimeout_DeniesBeforeAnyProvider(bool historical, long ticks)
    {
        var fixture = Service(Events());
        PartyIdentityOptions configured = PolicyOptions();
        configured.QueryTimeout = TimeSpan.FromTicks(ticks);
        fixture.Options.CurrentValue.Returns(configured);
        await AssertUnavailableAsync(fixture.Service, historical);
        fixture.Authority.DidNotReceiveWithAnyArgs().Admit(default(QueryEnvelope)!);
        await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
        await fixture.HistoryReader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default);
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    /// <summary>Caller cancellation wins a concurrent deadline/fault and preserves the original token.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CallerCancellationRacesDeadlineAndFault_PreservesOriginalToken(bool historical, bool expire)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.SetResult(); return pending.Task; });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        Task result = InvokeIdentityAsync(service, historical, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await caller.CancelAsync();
        if (expire) { advance(TimeSpan.FromSeconds(30), true); }
        pending.SetException(new HttpRequestException("synthetic racing fault"));
        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        exception.CancellationToken.ShouldBe(caller.Token);
    }

    /// <summary>Provider cancellation callbacks cannot hold deadline or caller result completion hostage.</summary>
    [Theory]
    [InlineData(false, false, "timer")]
    [InlineData(true, false, "timer")]
    [InlineData(false, true, "timer")]
    [InlineData(true, true, "timer")]
    [InlineData(false, false, "elapsed")]
    [InlineData(true, false, "elapsed")]
    [InlineData(false, true, "elapsed")]
    [InlineData(true, true, "elapsed")]
    [InlineData(false, false, "caller")]
    [InlineData(true, false, "caller")]
    [InlineData(false, true, "caller")]
    [InlineData(true, true, "caller")]
    public async Task BlockingProviderCancellationCallback_DoesNotDelayQueryCompletion(bool historical, bool inCustody, string trigger)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var providerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken providerToken = default;
        Action complete;
        if (inCustody)
        {
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
                .Returns(call => { providerToken = call.Arg<CancellationToken>(); providerEntered.TrySetResult(); return pending.Task; });
            complete = () => pending.TrySetResult(true);
        }
        else if (historical)
        {
            RetainedIdentityHistoryReadResult valid = await fixture.HistoryReader.ReadAsync(new("tenant-a", "party", "party-1"), RetainedIdentityHistoryReadRequest.AttributionPurpose, TestContext.Current.CancellationToken);
            var pending = new TaskCompletionSource<RetainedIdentityHistoryReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.HistoryReader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => { providerToken = call.Arg<CancellationToken>(); providerEntered.TrySetResult(); return pending.Task; });
            complete = () => pending.TrySetResult(valid);
        }
        else
        {
            AuthoritativeStreamReadResult valid = await fixture.Reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
            var pending = new TaskCompletionSource<AuthoritativeStreamReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(call => { providerToken = call.Arg<CancellationToken>(); providerEntered.TrySetResult(); return pending.Task; });
            complete = () => pending.TrySetResult(valid);
        }

        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        Task result = trigger == "caller" ? InvokeIdentityAsync(service, historical, caller.Token) : AssertUnavailableAsync(service, historical);
        await providerEntered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        // Register after the query suspended, making this provider callback the last callback
        // registered on its token. It remains blocked until the test's finally releases it.
        using CancellationTokenRegistration registration = providerToken.Register(() =>
        {
            cancellationEntered.TrySetResult();
            release.Wait(TestContext.Current.CancellationToken);
            cancellationReturned.TrySetResult();
        });
        Task cancellation = Task.CompletedTask;
        try
        {
            if (trigger == "elapsed")
            {
                advance(TimeSpan.FromSeconds(30), false);
                complete();
            }
            else
            {
                cancellation = Task.Run(() =>
                {
                    if (trigger == "caller") { caller.Cancel(); }
                    else { advance(TimeSpan.FromSeconds(30), true); }
                }, TestContext.Current.CancellationToken);
            }

            await cancellationEntered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            if (trigger == "caller")
            {
                OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
                exception.CancellationToken.ShouldBe(caller.Token);
            }
            else
            {
                await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }

            cancellationReturned.Task.IsCompleted.ShouldBeFalse();
            providerToken.IsCancellationRequested.ShouldBeTrue();
            if (!inCustody)
            {
                await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
            }
        }
        finally
        {
            release.Set();
            complete();
            await cancellationReturned.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await cancellation.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            try { await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken); }
            catch (OperationCanceledException) when (caller.IsCancellationRequested) { }
        }
    }

    /// <summary>Initial options access consumes the same budget instead of resetting the source timer.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task WholeQueryDeadline_SetupTimeConsumesOperationBudget(bool historical, bool exhausted)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        PartyIdentityOptions configured = PolicyOptions();
        configured.QueryTimeout = TimeSpan.FromSeconds(10);
        int optionsReads = 0;
        fixture.Options.CurrentValue.Returns(_ =>
        {
            if (++optionsReads == 1) { advance(TimeSpan.FromSeconds(exhausted ? 10 : 7), false); }
            return configured;
        });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.SetResult(); return pending.Task; });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        Task result = AssertUnavailableAsync(service, historical);
        if (!exhausted)
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            advance(TimeSpan.FromSeconds(2), true);
            result.IsCompleted.ShouldBeFalse();
            advance(TimeSpan.FromSeconds(1), true);
        }

        await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        if (exhausted)
        {
            fixture.Authority.DidNotReceiveWithAnyArgs().Admit(default(QueryEnvelope)!);
            await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
            await fixture.HistoryReader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default);
        }
        else
        {
            clock.Received(1).CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan);
        }

        pending.TrySetResult(true);
    }

    /// <summary>Caller cancellation before verdict release wins an already-started deadline or dependency fault.</summary>
    [Theory]
    [InlineData(false, "deadline")]
    [InlineData(true, "deadline")]
    [InlineData(false, "fault")]
    [InlineData(true, "fault")]
    public async Task CallerCancellationAfterDeadlineOrFaultBeforeVerdict_PreservesOriginalToken(bool historical, string trigger)
    {
        var fixture = Service(Events());
        using var caller = new CancellationTokenSource();
        using var releaseVerdict = new ManualResetEventSlim();
        var disposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (clock, advance) = DeadlineClock(() =>
        {
            disposing.TrySetResult();
            releaseVerdict.Wait(TestContext.Current.CancellationToken);
        });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.SetResult(); return pending.Task; });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        Task result = InvokeIdentityAsync(service, historical, caller.Token);
        Task failure = Task.CompletedTask;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            failure = Task.Run(() =>
            {
                if (trigger == "deadline") { advance(TimeSpan.FromSeconds(30), true); }
                else { pending.SetException(new HttpRequestException("synthetic fault before caller cancellation")); }
            }, TestContext.Current.CancellationToken);
            // Timer disposal holds either deadline cancellation or failed-await cleanup
            // after the failure starts and before the query can release a verdict.
            await disposing.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            result.IsCompleted.ShouldBeFalse();
            await caller.CancelAsync();
        }
        finally
        {
            releaseVerdict.Set();
            pending.TrySetResult(true);
        }

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        exception.CancellationToken.ShouldBe(caller.Token);
        await failure.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
    }

    /// <summary>A shortened budget accepts valid evidence immediately before its exclusive deadline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShortQueryDeadline_ImmediatelyBeforeExpiryStillResolves(bool historical)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        PartyIdentityOptions configured = PolicyOptions();
        configured.QueryTimeout = TimeSpan.FromSeconds(5);
        fixture.Options.CurrentValue.Returns(configured);
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => { advance(TimeSpan.FromSeconds(5) - TimeSpan.FromTicks(1), false); return true; });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        if (historical)
        {
            HumanActorBindingResult result = await service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
            result.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
            result.Evidence!.ActorId.ShouldBe(Actor);
            result.SourcePosition.ShouldBe(2);
            result.BindingSourcePosition.ShouldBe(2);
            result.ObservationId.ShouldBe("history-observation");
        }
        else
        {
            PartyIdentityResult result = await service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
            result.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
            result.Evidence!.HumanBinding!.ActorId.ShouldBe(Actor);
            result.Evidence.SourcePosition.ShouldBe(2);
        }
    }

    /// <summary>A delayed timer cannot release evidence after final authority consumes the last budget instant.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WholeQueryDeadline_FinalAuthorityExpiryDeniesTerminalEvidence(bool historical)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        PartyIdentityAdmissionResult admitted = fixture.Authority.Admit(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)));
        int authorityCalls = 0;
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(_ =>
        {
            if (++authorityCalls == (historical ? 3 : 2)) { advance(TimeSpan.FromSeconds(30), false); }
            return admitted;
        });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        await AssertUnavailableAsync(service, historical);
        authorityCalls.ShouldBe(historical ? 3 : 2);
        TestContext.Current.CancellationToken.IsCancellationRequested.ShouldBeFalse();
    }

    /// <summary>A late failing provider callback remains independent of the completed safe verdict.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderCancellationCallbackFaultAfterDeadline_DoesNotAffectVerdict(bool historical)
    {
        var fixture = Service(Events());
        var (clock, advance) = DeadlineClock();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken providerToken = default;
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(call => { providerToken = call.Arg<CancellationToken>(); entered.SetResult(); return pending.Task; });
        var service = new PartyIdentityQueryService(fixture.Authority, clock, fixture.Reader, fixture.Custody, fixture.HistoryReader, fixture.Options);
        Task result = AssertUnavailableAsync(service, historical);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        using CancellationTokenRegistration registration = providerToken.Register(() =>
        {
            cancelEntered.TrySetResult();
            release.Wait(TestContext.Current.CancellationToken);
            cancelReturned.TrySetResult();
            throw new InvalidOperationException("synthetic late provider cancellation fault");
        });
        try
        {
            advance(TimeSpan.FromSeconds(30), true);
            await cancelEntered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            cancelReturned.Task.IsCompleted.ShouldBeFalse();
            providerToken.IsCancellationRequested.ShouldBeTrue();
        }
        finally
        {
            release.Set();
            pending.TrySetResult(true);
            await cancelReturned.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }

        result.IsCompletedSuccessfully.ShouldBeTrue();
    }

    private static (TimeProvider Clock, Action<TimeSpan, bool> Advance) DeadlineClock(Action? onDispose = null)
    {
        TimeProvider clock = Substitute.For<TimeProvider>();
        DateTimeOffset now = DateTimeOffset.UtcNow.AddSeconds(1);
        long timestamp = 0;
        TimerCallback? callback = null;
        object? state = null;
        long dueAt = long.MaxValue;
        clock.TimestampFrequency.Returns(TimeSpan.TicksPerSecond);
        clock.GetTimestamp().Returns(_ => Interlocked.Read(ref timestamp));
        clock.GetUtcNow().Returns(_ => now + TimeSpan.FromTicks(Interlocked.Read(ref timestamp)));
        clock.CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>()).Returns(call =>
        {
            callback = call.Arg<TimerCallback>();
            state = call.ArgAt<object?>(1);
            dueAt = Interlocked.Read(ref timestamp) + call.ArgAt<TimeSpan>(2).Ticks;
            ITimer timer = Substitute.For<ITimer>();
            if (onDispose is not null) { timer.When(item => item.Dispose()).Do(_ => onDispose()); }
            return timer;
        });
        return (clock, (elapsed, fire) =>
        {
            long current = Interlocked.Add(ref timestamp, elapsed.Ticks);
            if (fire && current >= dueAt) { callback!(state); }
        });
    }

    private static void ConfigureSystemTimer(TimeProvider clock)
    {
        clock.TimestampFrequency.Returns(TimeProvider.System.TimestampFrequency);
        clock.GetTimestamp().Returns(_ => TimeProvider.System.GetTimestamp());
        clock.CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>()).Returns(call =>
            TimeProvider.System.CreateTimer(call.Arg<TimerCallback>(), call.ArgAt<object?>(1), call.ArgAt<TimeSpan>(2), call.ArgAt<TimeSpan>(3)));
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
