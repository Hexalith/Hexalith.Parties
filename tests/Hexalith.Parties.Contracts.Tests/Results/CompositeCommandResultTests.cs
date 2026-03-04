using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
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
}
