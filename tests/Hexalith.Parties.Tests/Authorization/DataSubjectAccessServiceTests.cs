using Hexalith.Parties.Authorization;

using Shouldly;

namespace Hexalith.Parties.Tests.Authorization;

public sealed class DataSubjectAccessServiceTests {
    [Theory]
    [InlineData("p1", "p1", true, DataSubjectAccessDenialReason.None)]
    [InlineData("p1", "p2", false, DataSubjectAccessDenialReason.AggregateMismatch)]
    [InlineData("P1", "p1", false, DataSubjectAccessDenialReason.AggregateMismatch)] // Ordinal — case-sensitive
    [InlineData(null, "p1", false, DataSubjectAccessDenialReason.MissingPartyBinding)]
    [InlineData("", "p1", false, DataSubjectAccessDenialReason.MissingPartyBinding)]
    [InlineData("   ", "p1", false, DataSubjectAccessDenialReason.MissingPartyBinding)]
    [InlineData("p1", null, false, DataSubjectAccessDenialReason.MissingAggregateId)]
    [InlineData("p1", "", false, DataSubjectAccessDenialReason.MissingAggregateId)]
    [InlineData("p1", "   ", false, DataSubjectAccessDenialReason.MissingAggregateId)]
    public void CheckSelfAccessDecidesFailClosed(
        string? boundPartyId,
        string? aggregateId,
        bool expectedAllowed,
        DataSubjectAccessDenialReason expectedReason) {
        IDataSubjectAccessService service = new DataSubjectAccessService();

        DataSubjectAccessDecision decision = service.CheckSelfAccess(boundPartyId, aggregateId);

        decision.IsAllowed.ShouldBe(expectedAllowed);
        decision.Reason.ShouldBe(expectedReason);
    }

    [Fact]
    public void AllowedDecisionExposesTheCanonicalAllowedSingleton() {
        IDataSubjectAccessService service = new DataSubjectAccessService();

        DataSubjectAccessDecision decision = service.CheckSelfAccess("p1", "p1");

        decision.ShouldBe(DataSubjectAccessDecision.Allowed);
        decision.DiagnosticText.ShouldBeNull();
    }
}
