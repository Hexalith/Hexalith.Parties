using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public sealed class PartyAggregateConsentRenewalTests
{
    private const string ConsentId = "ch-email-1:marketing";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Commands_RenewAndRevokeRepeatedly_PreserveEveryLifecycle(bool restricted)
    {
        PartyState state = restricted
            ? PartyTestData.CreateRestrictedState()
            : PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        PartyDetail detail = new() { Id = PartyTestData.DefaultPartyId, Type = PartyType.Person, DisplayName = "Test", SortName = "Test" };
        for (int cycle = 0; cycle < 3; cycle++)
        {
            ConsentRecord[] history = [.. state.ConsentRecords];
            DomainResult grant = PartyAggregate.Handle(PartyTestData.ValidRecordConsent() with
            {
                ActorUserId = $"grant-{cycle}", Source = $"grant-source-{cycle}",
            }, state);
            grant.IsSuccess.ShouldBeTrue();
            ConsentRecorded recorded = grant.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRecorded>();
            recorded.ConsentId.ShouldBe(ConsentId);
            state.Apply(recorded);
            detail = PartyDetailProjectionHandler.Apply(detail.Id, recorded, detail).ShouldNotBeNull();
            state.ConsentRecords.Take(cycle).ShouldBe(history);
            state.ConsentRecords.Count.ShouldBe(cycle + 1);
            state.ConsentRecords.Last().IsActive.ShouldBeTrue();
            detail.ConsentRecords.ShouldBe(state.ConsentRecords);
            PartyAggregate.Handle(PartyTestData.ValidRecordConsent(), state).IsNoOp.ShouldBeTrue();

            DomainResult revoke = PartyAggregate.Handle(PartyTestData.ValidRevokeConsent(ConsentId) with
            {
                ActorUserId = $"revoke-{cycle}", Reason = $"reason-{cycle}", Source = $"revoke-source-{cycle}",
            }, state);
            revoke.IsSuccess.ShouldBeTrue();
            ConsentRevoked revoked = revoke.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRevoked>();
            state.Apply(revoked);
            detail = PartyDetailProjectionHandler.Apply(detail.Id, revoked, detail).ShouldNotBeNull();
            state.ConsentRecords.Take(cycle).ShouldBe(history);
            ConsentRecord current = state.ConsentRecords.Last();
            current.IsActive.ShouldBeFalse();
            current.GrantedAt.ShouldBe(recorded.GrantedAt);
            current.GrantedBy.ShouldBe(recorded.GrantedBy);
            current.Source.ShouldBe(recorded.Source);
            current.RevokedAt.ShouldBe(revoked.RevokedAt);
            current.RevokedBy.ShouldBe(revoked.RevokedBy);
            current.RevocationReason.ShouldBe(revoked.Reason);
            current.RevocationSource.ShouldBe(revoked.Source);
            detail.ConsentRecords.ShouldBe(state.ConsentRecords);
            DomainResult retry = PartyAggregate.Handle(PartyTestData.ValidRevokeConsent(ConsentId), state);
            retry.IsNoOp.ShouldBeTrue();
            retry.Events.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Apply_ExactHistoricalReplay_DoesNotReopenOrRevokeRenewal(bool clockRegressed)
    {
        PartyState state = PartyTestData.CreatePersonState();
        ConsentRecorded first = Grant(0);
        ConsentRevoked firstRevoke = Revocation(1);
        ConsentRecorded renewal = Grant(clockRegressed ? -1 : 2) with { GrantedBy = "renewing-user", Source = "renewal" };
        state.Apply(first);
        state.Apply(firstRevoke);
        ConsentRecord historical = state.ConsentRecords.ShouldHaveSingleItem();
        state.Apply(first); // exact original grant replay after withdrawal
        state.ConsentRecords.ShouldHaveSingleItem().ShouldBe(historical);
        state.Apply(renewal);
        ConsentRecord active = state.ConsentRecords.Last();
        state.Apply(firstRevoke); // exact old withdrawal must not close the renewal
        state.Apply(renewal);
        state.ConsentRecords.ShouldBe([historical, active]);
        state.ConsentRecords.Last().IsActive.ShouldBeTrue();

        ConsentRevoked final = Revocation(3) with { RevokedBy = "renewing-user", Reason = "final" };
        state.Apply(final);
        ConsentRecord[] expected = [.. state.ConsentRecords];
        state.Apply(final with { RevokedAt = final.RevokedAt.AddSeconds(1), Reason = "retry" });
        state.ConsentRecords.ShouldBe(expected); // no rewrite when already revoked

        PartyState replay = new();
        replay.Apply(first);
        replay.Apply(firstRevoke);
        replay.Apply(renewal);
        replay.Apply(final);
        replay.ConsentRecords.ShouldBe(expected);
        replay.ConsentRecords[0].ShouldBe(historical);
        replay.ConsentRecords.ShouldAllBe(c => !c.IsActive);
    }

    [Fact]
    public void Apply_EquivalentTimestampOffsets_AreReplay()
    {
        PartyState state = new();
        ConsentRecorded original = Grant(0);
        ConsentRevoked revoked = Revocation(1);
        state.Apply(original);
        state.Apply(revoked);
        ConsentRecord historical = state.ConsentRecords.ShouldHaveSingleItem();

        state.Apply(original with { GrantedAt = original.GrantedAt.ToOffset(TimeSpan.FromHours(2)) });
        state.ConsentRecords.ShouldHaveSingleItem().ShouldBe(historical);
        state.ConsentRecords[0].GrantedAt.Offset.ShouldBe(TimeSpan.Zero);
        state.Apply(Grant(2));
        ConsentRecord renewal = state.ConsentRecords.Last();
        state.Apply(revoked with { RevokedAt = revoked.RevokedAt.ToOffset(TimeSpan.FromHours(-2)) });
        state.ConsentRecords.ShouldBe([historical, renewal]);
        renewal.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Apply_ActiveSameOwnerRetry_PreservesOriginalGrant()
    {
        PartyState state = new();
        state.Apply(Grant(0));
        ConsentRecord original = state.ConsentRecords.ShouldHaveSingleItem();
        state.Apply(Grant(1) with { Purpose = "support", LawfulBasis = LawfulBasis.ContractualNecessity });
        state.ConsentRecords.ShouldHaveSingleItem().ShouldBe(original);
    }

    [Fact]
    public void Revoke_LegacyRepeatedActiveRows_ClosesAllWithoutRewritingHistory()
    {
        PartyState state = PartyTestData.CreatePersonState();
        state.Apply(Grant(0));
        state.Apply(Revocation(1));
        ConsentRecord historical = state.ConsentRecords[0];
        state.Apply(Grant(2));
        // Model a persisted pre-fix state containing repeated active rows.
        var legacyRows = (List<ConsentRecord>)state.ConsentRecords;
        legacyRows.Add(legacyRows.Last() with { GrantedAt = DateTimeOffset.UnixEpoch.AddSeconds(3), Source = "legacy-repeat" });
        legacyRows.Add(legacyRows.Last());
        PartyDetail detail = new() { Id = PartyTestData.DefaultPartyId, Type = PartyType.Person, DisplayName = "Test", SortName = "Test", ConsentRecords = [.. legacyRows] };

        DomainResult result = PartyAggregate.Handle(PartyTestData.ValidRevokeConsent(ConsentId), state);

        result.IsSuccess.ShouldBeTrue();
        ConsentRevoked revoked = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRevoked>();
        state.Apply(revoked);
        detail = PartyDetailProjectionHandler.Apply(detail.Id, revoked, detail).ShouldNotBeNull();
        state.ConsentRecords.Count.ShouldBe(4);
        state.ConsentRecords[0].ShouldBe(historical);
        state.ConsentRecords.ShouldAllBe(c => !c.IsActive);
        state.ConsentRecords.Skip(1).ShouldAllBe(c => c.RevokedAt == revoked.RevokedAt && c.RevocationReason == revoked.Reason);
        detail.ConsentRecords.ShouldBe(state.ConsentRecords);
        PartyAggregate.Handle(PartyTestData.ValidRevokeConsent(ConsentId), state).IsNoOp.ShouldBeTrue();

        PartyState canonical = new();
        canonical.Apply(Grant(0));
        canonical.Apply(Revocation(1));
        canonical.Apply(Grant(2));
        canonical.Apply(Grant(3) with { Source = "legacy-repeat" });
        canonical.Apply(Grant(3) with { Source = "legacy-repeat" });
        canonical.Apply(revoked);
        // Full replay canonicalizes active retries; retained legacy rows remain audit
        // evidence until rebuild, but withdrawal closes every one in both histories.
        canonical.ConsentRecords.Count.ShouldBe(2);
        canonical.ConsentRecords.ShouldBe(state.ConsentRecords.Take(2));
        canonical.ConsentRecords.ShouldAllBe(c => !c.IsActive);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HistoricalForeignOwner_AlwaysBlocksGrantAndRevoke(bool foreignFirst, bool foreignRevoked)
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        ConsentRecorded foreign = Grant(0) with { ChannelId = "Ch-Email-1" };
        if (foreignFirst)
        {
            state.Apply(foreign);
            if (foreignRevoked)
            {
                state.Apply(Revocation(1));
            }
        }

        state.Apply(Grant(2));
        if (!foreignFirst)
        {
            state.Apply(Revocation(3));
            state.Apply(foreign);
            if (foreignRevoked)
            {
                var rows = (List<ConsentRecord>)state.ConsentRecords;
                rows[^1] = rows[^1] with { RevokedAt = DateTimeOffset.UnixEpoch.AddSeconds(1) };
            }
        }

        // The foreign channel is absent from current contact channels in every case.
        ConsentRecord[] original = [.. state.ConsentRecords];
        foreach (DomainResult result in new[]
        {
            PartyAggregate.Handle(PartyTestData.ValidRecordConsent(), state),
            PartyAggregate.Handle(PartyTestData.ValidRevokeConsent(ConsentId), state),
        })
        {
            result.IsRejection.ShouldBeTrue();
            result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>()
                .Message.ShouldBe("Consent ID has ambiguous contact channel ownership.");
        }

        state.Apply(Revocation(4)); // historical event has no channel discriminator
        state.ConsentRecords.ShouldBe(original);
        PartyDetail detail = new() { Id = PartyTestData.DefaultPartyId, Type = PartyType.Person, DisplayName = "Test", SortName = "Test", ConsentRecords = original };
        PartyDetailProjectionHandler.Apply(detail.Id, Revocation(4), detail).ShouldBeNull();
        detail.ConsentRecords.ShouldBe(original);
    }

    private static ConsentRecorded Grant(int second) => new()
    {
        PartyId = PartyTestData.DefaultPartyId,
        TenantId = PartyTestData.DefaultTenantId,
        ConsentId = ConsentId,
        ChannelId = PartyTestData.DefaultChannelId,
        Purpose = "marketing",
        LawfulBasis = LawfulBasis.Consent,
        GrantedAt = DateTimeOffset.UnixEpoch.AddSeconds(second),
        GrantedBy = "grant-user",
        Source = "grant-source",
    };

    private static ConsentRevoked Revocation(int second) => new()
    {
        PartyId = PartyTestData.DefaultPartyId,
        TenantId = PartyTestData.DefaultTenantId,
        ConsentId = ConsentId,
        RevokedAt = DateTimeOffset.UnixEpoch.AddSeconds(second),
        RevokedBy = "revoke-user",
        Reason = "withdrawn",
        Source = "revoke-source",
    };
}
