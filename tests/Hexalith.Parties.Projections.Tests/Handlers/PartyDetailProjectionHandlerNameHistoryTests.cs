using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

public class PartyDetailProjectionHandlerNameHistoryTests
{
    private const string PartyId = PartyTestData.DefaultPartyId;

    // 7.11 — PartyCreated initializes NameHistory with one entry
    [Fact]
    public void Apply_PartyCreated_InitializesNameHistoryWithOneEntry()
    {
        PartyCreated @event = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, null);

        result.ShouldNotBeNull();
        result.NameHistory.Count.ShouldBe(1);
        result.NameHistory[0].DisplayName.ShouldBe("John Doe");
        result.NameHistory[0].SortName.ShouldBe("Doe, John");
        result.NameHistory[0].TriggeredBy.ShouldBe(nameof(PartyCreated));
        result.NameHistory[0].ChangedAt.ShouldNotBe(default);
    }

    [Fact]
    public void Apply_PartyCreated_WhenStateExists_DoesNotResetNameHistory()
    {
        PartyCreated createEvent = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };
        PartyDetail? state = PartyDetailProjectionHandler.Apply(PartyId, createEvent, null);
        state = state.ShouldNotBeNull();
        DateTimeOffset originalChangedAt = state.NameHistory[0].ChangedAt;

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, createEvent, state);

        result = result.ShouldNotBeNull();
        result.ShouldBe(state);
        result.NameHistory.Count.ShouldBe(1);
        result.NameHistory[0].ChangedAt.ShouldBe(originalChangedAt);
    }

    // 7.12 — PartyDisplayNameDerived appends to NameHistory
    [Fact]
    public void Apply_PartyDisplayNameDerived_AppendsToNameHistory()
    {
        PartyCreated createEvent = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };
        PartyDetail? state = PartyDetailProjectionHandler.Apply(PartyId, createEvent, null);

        PartyDisplayNameDerived nameEvent = new()
        {
            DisplayName = "Jane Smith",
            SortName = "Smith, Jane",
        };
        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, nameEvent, state);

        result.ShouldNotBeNull();
        result.NameHistory.Count.ShouldBe(2);
        result.NameHistory[0].DisplayName.ShouldBe("John Doe");
        result.NameHistory[1].DisplayName.ShouldBe("Jane Smith");
        result.NameHistory[1].TriggeredBy.ShouldBe(nameof(PartyDisplayNameDerived));
        result.DisplayName.ShouldBe("Jane Smith");
        result.SortName.ShouldBe("Smith, Jane");
    }

    // 7.13 — duplicate DisplayName (no change) does NOT append
    [Fact]
    public void Apply_PartyDisplayNameDerived_SameDisplayName_DoesNotAppend()
    {
        PartyCreated createEvent = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };
        PartyDetail? state = PartyDetailProjectionHandler.Apply(PartyId, createEvent, null);

        PartyDisplayNameDerived sameNameEvent = new()
        {
            DisplayName = "John Doe",
            SortName = "Doe, John",
        };
        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, sameNameEvent, state);

        result.ShouldNotBeNull();
        result.NameHistory.Count.ShouldBe(1); // No new entry added
        result.DisplayName.ShouldBe("John Doe");
    }

    // 7.14 — ApplyErasure clears NameHistory
    [Fact]
    public void ApplyErasure_ClearsNameHistory()
    {
        PartyCreated createEvent = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };
        PartyDetail? state = PartyDetailProjectionHandler.Apply(PartyId, createEvent, null);

        PartyDisplayNameDerived nameEvent = new()
        {
            DisplayName = "Jane Smith",
            SortName = "Smith, Jane",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, nameEvent, state);
        state.ShouldNotBeNull();
        state.NameHistory.Count.ShouldBe(2);

        PartyDetail? erasedResult = PartyDetailProjectionHandler.ApplyErasure(PartyId, state);

        erasedResult.ShouldNotBeNull();
        erasedResult.NameHistory.ShouldBeEmpty();
        erasedResult.IsErased.ShouldBeTrue();
    }

    // Additional: NameHistory preserves chronological order
    [Fact]
    public void Apply_MultipleNameChanges_PreservesChronologicalOrder()
    {
        PartyCreated createEvent = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };
        PartyDetail? state = PartyDetailProjectionHandler.Apply(PartyId, createEvent, null);

        PartyDisplayNameDerived nameEvent1 = new()
        {
            DisplayName = "Jane Smith",
            SortName = "Smith, Jane",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, nameEvent1, state);

        PartyDisplayNameDerived nameEvent2 = new()
        {
            DisplayName = "Alice Johnson",
            SortName = "Johnson, Alice",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, nameEvent2, state);

        state.ShouldNotBeNull();
        state.NameHistory.Count.ShouldBe(3);
        state.NameHistory[0].DisplayName.ShouldBe("John Doe");
        state.NameHistory[1].DisplayName.ShouldBe("Jane Smith");
        state.NameHistory[2].DisplayName.ShouldBe("Alice Johnson");
    }
}
