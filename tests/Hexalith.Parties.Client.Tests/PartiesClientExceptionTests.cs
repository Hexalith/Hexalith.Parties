using Shouldly;

namespace Hexalith.Parties.Client.Tests;

public sealed class PartiesClientExceptionTests
{
    [Fact]
    public void Constructor_WithProblemDetails_SetsAllProperties()
    {
        var exception = new PartiesClientException(
            status: 422,
            title: "Domain Rejection",
            type: "urn:hexalith:parties:rejection:PartyAlreadyActive",
            detail: "The party is already active.",
            correlationId: "corr-test");

        exception.Status.ShouldBe(422);
        exception.Title.ShouldBe("Domain Rejection");
        exception.Type.ShouldBe("urn:hexalith:parties:rejection:PartyAlreadyActive");
        exception.Detail.ShouldBe("The party is already active.");
        exception.CorrelationId.ShouldBe("corr-test");
        exception.Message.ShouldBe("The party is already active.");
    }

    [Fact]
    public void Constructor_WithNullDetail_UsesTitleAsMessage()
    {
        var exception = new PartiesClientException(
            status: 500,
            title: "Internal Server Error",
            type: null,
            detail: null,
            correlationId: null);

        exception.Message.ShouldBe("Internal Server Error");
    }

    [Fact]
    public void Constructor_Default_CreatesInstance()
    {
        var exception = new PartiesClientException();

        exception.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var exception = new PartiesClientException("Something went wrong");

        exception.Message.ShouldBe("Something went wrong");
    }

    [Fact]
    public void Constructor_WithMessageAndInner_SetsProperties()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new PartiesClientException("outer", inner);

        exception.Message.ShouldBe("outer");
        exception.InnerException.ShouldBeSameAs(inner);
    }
}
