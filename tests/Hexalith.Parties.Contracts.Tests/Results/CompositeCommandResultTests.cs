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
}
