using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateContactChannelTests
{
    [Fact]
    public void Handle_AddContactChannel_NullState_ReturnsPartyNotFound()
    {
        AddContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-1",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = false,
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_AddContactChannel_NonPreferred_EmitsSingleContactChannelAdded()
    {
        PartyState state = PartyTestData.CreatePersonState();

        AddContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-email",
            Type = ContactChannelType.Email,
            Value = "jane@example.com",
            IsPreferred = false,
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        ContactChannelAdded added = result.Events[0].ShouldBeOfType<ContactChannelAdded>();
        added.ContactChannelId.ShouldBe("ch-email");
        added.Type.ShouldBe(ContactChannelType.Email);
        added.Value.ShouldBe("jane@example.com");
        added.IsPreferred.ShouldBeFalse();
    }

    [Fact]
    public void Handle_AddContactChannel_DuplicateId_ReturnsNoOp()
    {
        PartyState state = PartyTestData.CreatePersonState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "ch-1",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
        });

        AddContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-1",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = false,
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Handle_AddContactChannel_UnsafeContactChannelId_ReturnsSupportSafeRejection()
    {
        PartyState state = PartyTestData.CreatePersonState();
        AddContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "contact/unsafe",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        CompositeOperationConflict rejection = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        rejection.Message.ShouldBe("Contact channel ID is invalid.");
        rejection.Message.ShouldNotBeNull().ShouldNotContain(command.ContactChannelId);
        result.Events.OfType<ContactChannelAdded>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_AddContactChannel_IsPreferred_EmitsAddedAndPreferredChanged()
    {
        PartyState state = PartyTestData.CreatePersonState();

        AddContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-2",
            Type = ContactChannelType.Email,
            Value = "jane@example.com",
            IsPreferred = true,
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeOfType<ContactChannelAdded>();
        result.Events[1].ShouldBeOfType<PreferredContactChannelChanged>();
    }

    [Fact]
    public void Handle_UpdateContactChannel_NullState_ReturnsPartyNotFound()
    {
        UpdateContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-1",
            Value = "new@example.com",
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_UpdateContactChannel_NotFound_ReturnsContactChannelNotFound()
    {
        PartyState state = PartyTestData.CreatePersonState();
        UpdateContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "missing",
            Value = "new@example.com",
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelNotFound>();
    }

    [Fact]
    public void Handle_UpdateContactChannel_UnsafeContactChannelId_ReturnsSupportSafeRejectionBeforeNotFound()
    {
        PartyState state = PartyTestData.CreatePersonState();
        UpdateContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "contact/unsafe",
            Value = "new@example.com",
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        CompositeOperationConflict rejection = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        rejection.Message.ShouldBe("Contact channel ID is invalid.");
        rejection.Message.ShouldNotBeNull().ShouldNotContain(command.ContactChannelId);
        result.Events.OfType<ContactChannelNotFound>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdateContactChannel_ValidUpdate_EmitsContactChannelUpdated()
    {
        PartyState state = PartyTestData.CreatePersonState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "ch-1",
            Type = ContactChannelType.Email,
            Value = "old@example.com",
        });

        UpdateContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-1",
            Value = "updated@example.com",
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        ContactChannelUpdated updated = result.Events[0].ShouldBeOfType<ContactChannelUpdated>();
        updated.ContactChannelId.ShouldBe("ch-1");
        updated.Value.ShouldBe("updated@example.com");
    }

    [Fact]
    public void Handle_UpdateContactChannel_AlreadyPreferredAndTypeChanges_EmitsPreferredChanged()
    {
        PartyState state = PartyTestData.CreatePersonState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "email-1",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = true,
        });
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "phone-1",
            Type = ContactChannelType.Phone,
            Value = "+33123456789",
            IsPreferred = true,
        });

        UpdateContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "email-1",
            Type = ContactChannelType.Phone,
            IsPreferred = true,
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        ContactChannelUpdated updated = result.Events[0].ShouldBeOfType<ContactChannelUpdated>();
        updated.Type.ShouldBe(ContactChannelType.Phone);
        result.Events[1].ShouldBeOfType<PreferredContactChannelChanged>();

        state.Apply(updated);
        state.Apply((PreferredContactChannelChanged)result.Events[1]);

        ContactChannel phoneFromUpdatedEmail = state.ContactChannels.Single(c => c.Id == "email-1");
        ContactChannel existingPhone = state.ContactChannels.Single(c => c.Id == "phone-1");
        phoneFromUpdatedEmail.IsPreferred.ShouldBeTrue();
        existingPhone.IsPreferred.ShouldBeFalse();
    }

    [Fact]
    public void Handle_RemoveContactChannel_NullState_ReturnsPartyNotFound()
    {
        RemoveContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-1",
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_RemoveContactChannel_NotFound_ReturnsContactChannelNotFound()
    {
        PartyState state = PartyTestData.CreatePersonState();
        RemoveContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "missing",
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelNotFound>();
    }

    [Fact]
    public void Handle_RemoveContactChannel_UnsafeContactChannelId_ReturnsSupportSafeRejectionBeforeNotFound()
    {
        PartyState state = PartyTestData.CreatePersonState();
        RemoveContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "contact/unsafe",
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        CompositeOperationConflict rejection = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        rejection.Message.ShouldBe("Contact channel ID is invalid.");
        rejection.Message.ShouldNotBeNull().ShouldNotContain(command.ContactChannelId);
        result.Events.OfType<ContactChannelNotFound>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_RemoveContactChannel_Existing_EmitsRemoved()
    {
        PartyState state = PartyTestData.CreatePersonState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "ch-remove",
            Type = ContactChannelType.Email,
            Value = "to-remove@example.com",
        });

        RemoveContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-remove",
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelRemoved>();
    }

    [Fact]
    public void Handle_AddContactChannel_With50ExistingChannels_StillSucceeds()
    {
        PartyState state = PartyTestData.CreatePersonState();
        for (int i = 0; i < 50; i++)
        {
            state.Apply(new ContactChannelAdded
            {
                ContactChannelId = $"ch-{i}",
                Type = ContactChannelType.Email,
                Value = $"user{i}@example.com",
            });
        }

        AddContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-51",
            Type = ContactChannelType.Phone,
            Value = "+33600000051",
            IsPreferred = false,
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Handle_UpdateContactChannel_With50ExistingChannels_StillSucceeds()
    {
        PartyState state = PartyTestData.CreatePersonState();
        for (int i = 0; i < 50; i++)
        {
            state.Apply(new ContactChannelAdded
            {
                ContactChannelId = $"ch-{i}",
                Type = ContactChannelType.Email,
                Value = $"user{i}@example.com",
            });
        }

        UpdateContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-0",
            Value = "updated@example.com",
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelUpdated>();
    }

    [Fact]
    public void Handle_RemoveContactChannel_With50ExistingChannels_StillSucceeds()
    {
        PartyState state = PartyTestData.CreatePersonState();
        for (int i = 0; i < 50; i++)
        {
            state.Apply(new ContactChannelAdded
            {
                ContactChannelId = $"ch-{i}",
                Type = ContactChannelType.Email,
                Value = $"user{i}@example.com",
            });
        }

        RemoveContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-25",
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelRemoved>();
    }

    [Fact]
    public void Handle_AddContactChannel_With50ExistingChannels_AndPreferred_StillSucceeds()
    {
        PartyState state = PartyTestData.CreatePersonState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "phone-existing",
            Type = ContactChannelType.Phone,
            Value = "+33600000000",
            IsPreferred = true,
        });

        for (int i = 0; i < 50; i++)
        {
            state.Apply(new ContactChannelAdded
            {
                ContactChannelId = $"ch-{i}",
                Type = ContactChannelType.Email,
                Value = $"user{i}@example.com",
            });
        }

        AddContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "phone-new-preferred",
            Type = ContactChannelType.Phone,
            Value = "+33600000051",
            IsPreferred = true,
        };

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);

        ContactChannelAdded added = result.Events[0].ShouldBeOfType<ContactChannelAdded>();
        added.ContactChannelId.ShouldBe("phone-new-preferred");
        added.IsPreferred.ShouldBeTrue();

        PreferredContactChannelChanged preferredChanged = result.Events[1].ShouldBeOfType<PreferredContactChannelChanged>();
        preferredChanged.ContactChannelId.ShouldBe("phone-new-preferred");

        state.Apply(added);
        state.Apply(preferredChanged);

        ContactChannel existingPhone = state.ContactChannels.Single(c => c.Id == "phone-existing");
        ContactChannel newPhone = state.ContactChannels.Single(c => c.Id == "phone-new-preferred");
        existingPhone.IsPreferred.ShouldBeFalse();
        newPhone.IsPreferred.ShouldBeTrue();
    }
}
