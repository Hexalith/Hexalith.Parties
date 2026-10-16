using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

[Collection("Non-parallel")]
public sealed class PartyAggregateChannelIdCollisionTests
{
    private const string UpperId = "Ch-Email-1";
    private const string LowerId = "ch-email-1";
    private const string ConsentId = "ch-email-1:marketing";

    [Theory]
    [InlineData(UpperId, LowerId)]
    [InlineData(LowerId, UpperId)]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890", "A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    public void Add_CaseOnlyDuplicate_RejectsWithoutChangingState(string existingId, string addedId)
    {
        PartyState state = StateWithChannels(existingId);

        DomainResult result = PartyAggregate.Handle(Add(addedId), state);

        AssertChannelConflict(result);
        state.ContactChannels.ShouldHaveSingleItem().Id.ShouldBe(existingId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_ExactRetry_RemainsNoOp(bool historicallyAmbiguous)
    {
        PartyState state = historicallyAmbiguous ? StateWithChannels(UpperId, LowerId) : StateWithChannels(UpperId);

        DomainResult result = PartyAggregate.Handle(Add(UpperId), state);

        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateComposite_ExactRetriesOfHistoricalVariants_RemainSkipped()
    {
        PartyState state = StateWithChannels(UpperId, LowerId);

        CompositeCommandResult result = PartyAggregate.Handle(new UpdatePartyComposite
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddContactChannels = [Add(UpperId), Add(LowerId)],
        }, state);

        result.IsNoOp.ShouldBeTrue();
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldBeEmpty();
        result.Applied.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
        result.Skipped.ShouldBe([$"Duplicate contact channel: {UpperId}", $"Duplicate contact channel: {LowerId}"]);
        state.ContactChannels.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(UpperId, LowerId)]
    [InlineData(LowerId, UpperId)]
    public void CreateComposite_CaseOnlyDuplicate_RejectsEntireCreation(string firstId, string secondId)
    {
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite() with
        {
            ContactChannels = [Add(firstId), Add(secondId) with { IsPreferred = true }],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        AssertCompositeConflict(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpdateComposite_CaseOnlyAddition_RejectsAllChanges(bool collisionWithinPayload)
    {
        PartyState state = collisionWithinPayload ? StateWithChannels() : StateWithChannels(UpperId);
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            PersonDetails = new PersonDetails { FirstName = "Changed", LastName = "Name" },
            AddContactChannels = collisionWithinPayload
                ? [Add(UpperId), Add(LowerId)]
                : [Add("new-channel"), Add(LowerId)],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        AssertCompositeConflict(result);
        state.Person!.FirstName.ShouldBe("John");
        state.ContactChannels.Count.ShouldBe(collisionWithinPayload ? 0 : 1);
    }

    [Theory]
    [InlineData(UpperId)]
    [InlineData(LowerId)]
    public void Update_AmbiguousHistoricalChannel_RejectsBothVariants(string targetId)
    {
        PartyState state = StateWithChannels(UpperId, LowerId);

        DomainResult result = PartyAggregate.Handle(Update(targetId), state);

        AssertChannelConflict(result);
        state.ContactChannels.Count.ShouldBe(2);
        state.ContactChannels.ShouldAllBe(c => c.Value == "original@example.test");
    }

    [Fact]
    public void UpdateComposite_AmbiguousHistoricalChannel_RejectsBeforeDetailsOrRemoval()
    {
        PartyState state = StateWithChannels(UpperId, LowerId);
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            PersonDetails = new PersonDetails { FirstName = "Changed", LastName = "Name" },
            UpdateContactChannels = [Update(UpperId)],
            RemoveContactChannelIds = [LowerId],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        AssertCompositeConflict(result);
        state.Person!.FirstName.ShouldBe("John");
        state.ContactChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void HistoricalAmbiguity_UnrelatedChannelRemainsUsable()
    {
        PartyState state = StateWithChannels(UpperId, LowerId, "other");

        DomainResult updated = PartyAggregate.Handle(Update("other"), state);
        updated.IsSuccess.ShouldBeTrue();
        updated.Events.Count.ShouldBe(2);
        updated.Events[0].ShouldBeOfType<ContactChannelUpdated>().ContactChannelId.ShouldBe("other");
        updated.Events[1].ShouldBeOfType<PreferredContactChannelChanged>().ContactChannelId.ShouldBe("other");
        DomainResult added = PartyAggregate.Handle(Add("new-channel"), state);
        added.IsSuccess.ShouldBeTrue();
        DomainResult consent = PartyAggregate.Handle(Record("other"), state);
        consent.IsSuccess.ShouldBeTrue();
        consent.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRecorded>().ConsentId.ShouldBe("other:marketing");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove_ExactHistoricalVariant_RepairsChannelsWithoutRewritingConsent(bool composite)
    {
        PartyState state = StateWithChannels(UpperId, LowerId);
        state.Apply(Recorded(UpperId));
        ConsentRecord originalConsent = state.ConsentRecords.ShouldHaveSingleItem();
        ContactChannelRemoved removed;
        if (composite)
        {
            CompositeCommandResult result = PartyAggregate.Handle(new UpdatePartyComposite
            {
                PartyId = PartyTestData.DefaultPartyId,
                RemoveContactChannelIds = [LowerId],
            }, state);
            result.IsSuccess.ShouldBeTrue();
            result.UpdatedPartyDetail!.ContactChannels.ShouldHaveSingleItem().Id.ShouldBe(UpperId);
            removed = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ContactChannelRemoved>();
        }
        else
        {
            DomainResult result = PartyAggregate.Handle(new RemoveContactChannel
            {
                PartyId = PartyTestData.DefaultPartyId,
                ContactChannelId = LowerId,
            }, state);
            result.IsSuccess.ShouldBeTrue();
            removed = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ContactChannelRemoved>();
        }

        removed.ContactChannelId.ShouldBe(LowerId);
        state.Apply(removed);
        state.ContactChannels.ShouldHaveSingleItem().Id.ShouldBe(UpperId);
        state.ConsentRecords.ShouldHaveSingleItem().ShouldBe(originalConsent);
        PartyAggregate.Handle(Update(UpperId), state).IsSuccess.ShouldBeTrue();
        PartyAggregate.Handle(Record(UpperId), state).IsNoOp.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lookups_WrongCase_RemainNotFound(bool historicallyAmbiguous)
    {
        PartyState state = historicallyAmbiguous ? StateWithChannels(UpperId, LowerId) : StateWithChannels(UpperId);
        string absentId = historicallyAmbiguous ? "CH-EMAIL-1" : LowerId;

        PartyAggregate.Handle(Update(absentId), state).Events.ShouldHaveSingleItem().ShouldBeOfType<ContactChannelNotFound>();
        PartyAggregate.Handle(new RemoveContactChannel
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = absentId,
        }, state).Events.ShouldHaveSingleItem().ShouldBeOfType<ContactChannelNotFound>();
        PartyAggregate.Handle(Record(absentId), state).Events.ShouldHaveSingleItem().ShouldBeOfType<ContactChannelNotFound>();
        AssertCompositeNotFound(PartyAggregate.Handle(new UpdatePartyComposite
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels = [Update(absentId)],
        }, state));
        AssertCompositeNotFound(PartyAggregate.Handle(new UpdatePartyComposite
        {
            PartyId = PartyTestData.DefaultPartyId,
            RemoveContactChannelIds = [absentId],
        }, state));
    }

    [Fact]
    public void CollisionState_ErasureRejectionsKeepPrecedence()
    {
        PartyState state = StateWithChannels(UpperId, LowerId);
        state.Apply(Recorded(UpperId));
        state.Apply(Recorded(LowerId));
        state.Apply(new ErasePartyRequested
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            RequestedAt = DateTimeOffset.UtcNow,
            RequestedBy = "test-admin",
        });

        DomainResult[] results =
        [
            PartyAggregate.Handle(Add(UpperId), state),
            PartyAggregate.Handle(Update(UpperId), state),
            PartyAggregate.Handle(Record(UpperId), state),
            PartyAggregate.Handle(PartyTestData.ValidRevokeConsent(ConsentId), state),
        ];
        foreach (DomainResult result in results)
        {
            result.IsRejection.ShouldBeTrue();
            result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyErasureInProgress>();
        }

        CompositeCommandResult composite = PartyAggregate.Handle(new UpdatePartyComposite
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels = [Update(UpperId)],
        }, state);
        composite.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyErasureInProgress>();
        composite.Applied.ShouldBeEmpty();
    }

    [Fact]
    public void CollisionState_RestrictedChannelRejectionsKeepPrecedence()
    {
        PartyState state = PartyTestData.CreateRestrictedState();
        state.Apply(new ContactChannelAdded { ContactChannelId = UpperId, Type = ContactChannelType.Email, Value = "first@example.test" });

        DomainResult[] results = [PartyAggregate.Handle(Add(UpperId), state), PartyAggregate.Handle(Update(UpperId), state)];
        foreach (DomainResult result in results)
        {
            result.IsRejection.ShouldBeTrue();
            result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyProcessingRestricted>();
        }

        CompositeCommandResult composite = PartyAggregate.Handle(new UpdatePartyComposite
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddContactChannels = [Add("CH-EMAIL-1")],
        }, state);
        composite.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyProcessingRestricted>();
        composite.Applied.ShouldBeEmpty();
        AssertChannelConflict(PartyAggregate.Handle(Record(UpperId), state));
    }

    [Theory]
    [InlineData(UpperId, false)]
    [InlineData(LowerId, false)]
    [InlineData(UpperId, true)]
    [InlineData(LowerId, true)]
    public void Record_AmbiguousChannels_RejectsBeforeIdempotency(string channelId, bool existingConsent)
    {
        PartyState state = StateWithChannels(UpperId, LowerId);
        if (existingConsent)
        {
            state.Apply(Recorded(UpperId));
        }

        DomainResult result = PartyAggregate.Handle(Record(channelId), state);

        AssertChannelConflict(result);
        state.ConsentRecords.Count.ShouldBe(existingConsent ? 1 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Record_RemovedChannelStillOwnsGeneratedId_RejectsActiveOrRevokedCollision(bool revoked)
    {
        PartyState state = StateWithChannels(UpperId);
        state.Apply(Recorded(UpperId));
        if (revoked)
        {
            state.Apply(Revoked());
        }

        state.Apply(new ContactChannelRemoved { ContactChannelId = UpperId });
        DomainResult add = PartyAggregate.Handle(Add(LowerId), state);
        add.IsSuccess.ShouldBeTrue();
        state.Apply(add.Events.ShouldHaveSingleItem().ShouldBeOfType<ContactChannelAdded>());
        ConsentRecord original = state.ConsentRecords.ShouldHaveSingleItem();

        DomainResult result = PartyAggregate.Handle(Record(LowerId), state);

        AssertConsentConflict(result);
        state.ConsentRecords.ShouldHaveSingleItem().ShouldBe(original);
    }

    [Fact]
    public void Record_UnambiguousMixedCaseChannel_PreservesIdAndPurposeIdempotency()
    {
        PartyState state = StateWithChannels(UpperId);
        DomainResult first = PartyAggregate.Handle(Record(UpperId) with { Purpose = "  MarKeting  " }, state);
        first.IsSuccess.ShouldBeTrue();
        ConsentRecorded recorded = first.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRecorded>();
        recorded.ConsentId.ShouldBe(ConsentId);
        recorded.ChannelId.ShouldBe(UpperId);
        recorded.Purpose.ShouldBe("marketing");
        state.Apply(recorded);

        PartyAggregate.Handle(Record(UpperId) with { Purpose = "MARKETING" }, state).IsNoOp.ShouldBeTrue();
        DomainResult revoke = PartyAggregate.Handle(PartyTestData.ValidRevokeConsent(ConsentId), state);
        revoke.IsSuccess.ShouldBeTrue();
        revoke.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRevoked>().ConsentId.ShouldBe(ConsentId);
    }

    [Fact]
    public void Record_LaterForeignOwner_RejectsEvenWhenFirstRecordMatchesChannel()
    {
        PartyState state = StateWithChannels(UpperId);
        state.Apply(Recorded(UpperId));
        state.Apply(Revoked());
        state.Apply(Recorded(LowerId));
        state.ConsentRecords[0].ChannelId.ShouldBe(UpperId);
        state.ConsentRecords[0].IsActive.ShouldBeFalse();
        state.ConsentRecords[1].ChannelId.ShouldBe(LowerId);
        state.ConsentRecords[1].IsActive.ShouldBeTrue();

        AssertConsentConflict(PartyAggregate.Handle(Record(UpperId), state));
    }

    [Fact]
    public void Record_ReaddedExactChannel_RetainsConsentOwnershipAndIdempotency()
    {
        PartyState state = StateWithChannels(UpperId);
        state.Apply(Recorded(UpperId));
        ConsentRecord original = state.ConsentRecords.ShouldHaveSingleItem();
        DomainResult removed = PartyAggregate.Handle(new RemoveContactChannel
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = UpperId,
        }, state);
        removed.IsSuccess.ShouldBeTrue();
        state.Apply(removed.Events.ShouldHaveSingleItem().ShouldBeOfType<ContactChannelRemoved>());
        DomainResult added = PartyAggregate.Handle(Add(UpperId), state);
        added.IsSuccess.ShouldBeTrue();
        state.Apply(added.Events.ShouldHaveSingleItem().ShouldBeOfType<ContactChannelAdded>());

        PartyAggregate.Handle(Record(UpperId), state).IsNoOp.ShouldBeTrue();
        state.ConsentRecords.ShouldHaveSingleItem().ShouldBe(original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Revoke_DuplicateConsentIdWithDifferentOwners_RejectsBeforeSelectingFirst(bool firstRevoked)
    {
        PartyState state = StateWithChannels(UpperId, LowerId);
        state.Apply(Recorded(UpperId));
        if (firstRevoked)
        {
            state.Apply(Revoked());
        }

        state.Apply(Recorded(LowerId));
        ConsentRecord[] original = [.. state.ConsentRecords];

        DomainResult result = PartyAggregate.Handle(PartyTestData.ValidRevokeConsent(ConsentId), state);

        AssertConsentConflict(result);
        state.ConsentRecords.ShouldBe(original);
    }

    [Fact]
    public void Revoke_SingleConsentOwner_RemainsAllowedDespiteAmbiguousChannels()
    {
        PartyState state = StateWithChannels(UpperId, LowerId);
        state.Apply(Recorded(UpperId));
        state.Apply(new ProcessingRestricted
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            RestrictedAt = DateTimeOffset.UtcNow,
            Reason = "test",
            RestrictedBy = "test-admin",
        });

        DomainResult result = PartyAggregate.Handle(PartyTestData.ValidRevokeConsent(ConsentId), state);

        result.IsSuccess.ShouldBeTrue();
        ConsentRevoked revoked = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRevoked>();
        revoked.ConsentId.ShouldBe(ConsentId);
        state.Apply(revoked);
        state.ConsentRecords.ShouldHaveSingleItem().IsActive.ShouldBeFalse();
    }

    private static PartyState StateWithChannels(params string[] channelIds)
    {
        PartyState state = PartyTestData.CreatePersonState();
        foreach (string id in channelIds)
        {
            state.Apply(new ContactChannelAdded
            {
                ContactChannelId = id,
                Type = ContactChannelType.Email,
                Value = "original@example.test",
            });
        }

        return state;
    }

    private static AddContactChannel Add(string id) => new()
    {
        PartyId = PartyTestData.DefaultPartyId,
        ContactChannelId = id,
        Type = ContactChannelType.Email,
        Value = "new@example.test",
    };

    private static UpdateContactChannel Update(string id) => new()
    {
        PartyId = PartyTestData.DefaultPartyId,
        ContactChannelId = id,
        Value = "changed@example.test",
        IsPreferred = true,
    };

    private static RecordConsent Record(string id) => PartyTestData.ValidRecordConsent() with { ChannelId = id };

    private static ConsentRecorded Recorded(string id) => new()
    {
        PartyId = PartyTestData.DefaultPartyId,
        TenantId = PartyTestData.DefaultTenantId,
        ConsentId = ConsentId,
        ChannelId = id,
        Purpose = "marketing",
        LawfulBasis = LawfulBasis.Consent,
        GrantedAt = DateTimeOffset.UtcNow,
        GrantedBy = "test-admin",
    };

    private static ConsentRevoked Revoked() => new()
    {
        PartyId = PartyTestData.DefaultPartyId,
        TenantId = PartyTestData.DefaultTenantId,
        ConsentId = ConsentId,
        RevokedAt = DateTimeOffset.UtcNow,
        RevokedBy = "test-admin",
    };

    private static void AssertChannelConflict(DomainResult result)
    {
        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>()
            .Message.ShouldBe("Contact channel IDs must be unique ignoring case.");
    }

    private static void AssertConsentConflict(DomainResult result)
    {
        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>()
            .Message.ShouldBe("Consent ID has ambiguous contact channel ownership.");
    }

    private static void AssertCompositeConflict(CompositeCommandResult result)
    {
        result.IsSuccess.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>()
            .Message.ShouldBe("Contact channel IDs must be unique ignoring case.");
        result.Applied.ShouldBeEmpty();
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldHaveSingleItem();
        result.UpdatedPartyDetail.ShouldBeNull();
    }

    private static void AssertCompositeNotFound(CompositeCommandResult result)
    {
        result.IsSuccess.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<ContactChannelNotFound>();
        result.Applied.ShouldBeEmpty();
        result.UpdatedPartyDetail.ShouldBeNull();
    }
}
