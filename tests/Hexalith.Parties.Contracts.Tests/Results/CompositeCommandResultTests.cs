using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Results;

public class CompositeCommandResultTests
{
    [Fact]
    public void Constructor_WithEvents_SetsAllProperties()
    {
        IEventPayload[] events = [new PartyCreated { Type = PartyType.Person }];
        string[] applied = ["Created party"];
        string[] skipped = ["Skipped duplicate"];
        string[] rejected = [];

        var result = new CompositeCommandResult(events, applied, skipped, rejected);

        result.Events.Count.ShouldBe(1);
        result.Applied.Count.ShouldBe(1);
        result.Skipped.Count.ShouldBe(1);
        result.Rejected.ShouldBeEmpty();
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithRejectionEvents_IsRejection()
    {
        IEventPayload[] events = [new PartyCannotBeCreatedWithoutType()];
        string[] applied = [];
        string[] skipped = [];
        string[] rejected = ["Missing party type"];

        var result = new CompositeCommandResult(events, applied, skipped, rejected);

        result.IsRejection.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithNoEvents_IsNoOp()
    {
        var result = new CompositeCommandResult([], [], [], []);

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithUpdatedPartyDetail_SerializesCamelCaseStringEnumResultPayload()
    {
        IEventPayload[] events = [new PartyCreated { Type = PartyType.Person }];
        var detail = new PartyDetail
        {
            Id = "party-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
        };

        var result = new CompositeCommandResult(events, ["Created party"], [], [], detail);

        result.ResultPayload.ShouldNotBeNullOrWhiteSpace();
        result.ResultPayload.ShouldContain("\"displayName\":\"Ada Lovelace\"");
        result.ResultPayload.ShouldContain("\"type\":\"Person\"");
    }

    [Fact]
    public void PartyCommandResult_WithUpdatedPartyDetail_SerializesSamePayloadShape()
    {
        IEventPayload[] events = [new PartyCreated { Type = PartyType.Organization }];
        var detail = new PartyDetail
        {
            Id = "party-2",
            Type = PartyType.Organization,
            IsActive = true,
            DisplayName = "Acme Corp",
            SortName = "Acme Corp",
        };

        var result = new PartyCommandResult(events, detail);

        result.ResultPayload.ShouldNotBeNullOrWhiteSpace();
        result.ResultPayload.ShouldContain("\"displayName\":\"Acme Corp\"");
        result.ResultPayload.ShouldContain("\"type\":\"Organization\"");
    }

    [Fact]
    public void PartyCommandResult_ResultPayload_IsMemoizedAndPreservesValueEquality()
    {
        // ResultPayload is now serialized once at construction. Two value-equal results must expose
        // an equal payload, and reading the memoized payload on one must not break record equality
        // (the cache field is a private readonly field included in the synthesized Equals).
        IEventPayload[] events = [new PartyCreated { Type = PartyType.Person }];
        var detail = new PartyDetail
        {
            Id = "party-3",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Grace Hopper",
            SortName = "Hopper, Grace",
        };

        var first = new PartyCommandResult(events, detail);
        var second = new PartyCommandResult(events, detail);

        string firstPayload = first.ResultPayload.ShouldNotBeNull();
        string secondPayload = second.ResultPayload.ShouldNotBeNull();
        firstPayload.ShouldBe(secondPayload);
        firstPayload.ShouldContain("\"displayName\":\"Grace Hopper\"");

        // Equality must still hold after the memoized payload has been read.
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void CompositeCommandResult_WithNoUpdatedPartyDetail_ResultPayloadIsNull()
    {
        // Compatibility contract for callers ignoring `resultPayload`: when no enriched
        // detail is available (no-op, partial composite, rejection), `ResultPayload` must
        // be null so the existing correlationId/status-only response shape is preserved.
        IEventPayload[] events = [new PartyCreated { Type = PartyType.Person }];

        var result = new CompositeCommandResult(events, ["Created party"], [], [], updatedPartyDetail: null);

        result.IsSuccess.ShouldBeTrue();
        result.ResultPayload.ShouldBeNull();
    }

    [Fact]
    public void CompositeCommandResult_RejectionWithoutUpdatedPartyDetail_ResultPayloadIsNull()
    {
        IEventPayload[] events = [new PartyCannotBeCreatedWithoutType()];

        var result = new CompositeCommandResult(events, [], [], ["Missing party type"], updatedPartyDetail: null);

        result.IsRejection.ShouldBeTrue();
        result.ResultPayload.ShouldBeNull();
    }
}
