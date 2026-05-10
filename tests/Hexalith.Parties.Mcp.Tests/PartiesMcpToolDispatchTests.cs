using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Mcp.Tools;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Mcp.Tests;

public sealed class PartiesMcpToolDispatchTests
{
    private const string TenantId = "tenant-a";
    private const string UserId = "user-a";

    [Fact]
    public async Task GetPartyFailsClosedWhenTenantOrUserContextIsMissing()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        var tools = new PartiesMcpTools(commandClient, queryClient, new StubContextAccessor(null));

        PartiesMcpToolResult result = await tools.GetParty("party-1", CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("missing_context");
        result.Code.ShouldBe("parties-mcp-missing-context");
        await queryClient.DidNotReceiveWithAnyArgs().GetPartyAsync(default!, default);
    }

    [Fact]
    public async Task CreatePartyDispatchesCompositeCommandThroughTypedCommandClient()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.CreatePartyCompositeAsync(Arg.Any<CreatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns("corr-create");
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            partyId: "party-1",
            partyType: "person",
            givenName: "Ada",
            familyName: "Lovelace",
            legalName: null,
            email: "ada@example.test",
            phone: null,
            identifierType: "TaxId",
            identifierValue: "TAX-123",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        result.Category.ShouldBe("success");
        result.CorrelationId.ShouldBe("corr-create");

        await commandClient.Received(1).CreatePartyCompositeAsync(
            Arg.Is<CreatePartyComposite>(command =>
                command.PartyId == "party-1"
                && command.Type == PartyType.Person
                && command.PersonDetails!.FirstName == "Ada"
                && command.PersonDetails.LastName == "Lovelace"
                && command.ContactChannels.Count == 1
                && command.Identifiers.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyRejectsNoChangePatchBeforeCallingClient()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty("party-1", cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-no-change");
        await commandClient.DidNotReceiveWithAnyArgs().UpdatePartyCompositeAsync(default!, default!, default);
    }

    [Fact]
    public async Task UpdatePartyUsesRoutePartyIdAsAuthoritativeCompositeCommandId()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.UpdatePartyCompositeAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns("corr-update");
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "route-party",
            givenName: "Grace",
            familyName: "Hopper",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).UpdatePartyCompositeAsync(
            "route-party",
            Arg.Is<UpdatePartyComposite>(command =>
                command.PartyId == "route-party"
                && command.PersonDetails!.FirstName == "Grace"
                && command.PersonDetails.LastName == "Hopper"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyPreservesContactAndIdentifierPatchOperations()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.UpdatePartyCompositeAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns("corr-update");
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            addEmail: "new@example.test",
            updateContactChannelId: "contact-1",
            updateContactChannelType: "phone",
            updateContactChannelValue: "+33123456789",
            updateContactChannelPreferred: true,
            removeContactChannelId: "contact-old",
            addIdentifierType: "VAT",
            addIdentifierValue: "FR123",
            removeIdentifierId: "identifier-old",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).UpdatePartyCompositeAsync(
            "party-1",
            Arg.Is<UpdatePartyComposite>(command =>
                command.AddContactChannels.Single().Type == ContactChannelType.Email
                && command.UpdateContactChannels.Single().ContactChannelId == "contact-1"
                && command.UpdateContactChannels.Single().Type == ContactChannelType.Phone
                && command.RemoveContactChannelIds.Single() == "contact-old"
                && command.AddIdentifiers.Single().Type == IdentifierType.VAT
                && command.RemoveIdentifierIds.Single() == "identifier-old"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePartyIsIdempotentWhenPartyIsAlreadyInactive()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        queryClient.GetPartyAsync("party-1", Arg.Any<CancellationToken>())
            .Returns(new PartyDetail
            {
                Id = "party-1",
                Type = PartyType.Person,
                IsActive = false,
                DisplayName = "Inactive Person",
                SortName = "Person Inactive",
            });
        var tools = new PartiesMcpTools(commandClient, queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.DeleteParty("party-1", CancellationToken.None);

        result.Status.ShouldBe("succeeded");
        result.Category.ShouldBe("success");
        result.Code.ShouldBe("parties-mcp-delete-idempotent");
        await commandClient.DidNotReceiveWithAnyArgs().DeactivatePartyAsync(default!, default);
    }

    [Fact]
    public async Task FindPartiesUsesSearchWhenQueryIsProvidedAndListOtherwise()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.SearchPartiesAsync("ada", 2, 10, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<PartySearchResult> { Items = [], Page = 2, PageSize = 10 });
        queryClient.ListPartiesAsync(1, 20, PartyType.Organization, true, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<PartyIndexEntry> { Items = [], Page = 1, PageSize = 20 });
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        (await tools.FindParties("ada", 2, 10, null, null, CancellationToken.None)).Status.ShouldBe("succeeded");
        (await tools.FindParties(null, 1, 20, "organization", true, CancellationToken.None)).Status.ShouldBe("succeeded");

        await queryClient.Received(1).SearchPartiesAsync("ada", 2, 10, Arg.Any<CancellationToken>());
        await queryClient.Received(1).ListPartiesAsync(1, 20, PartyType.Organization, true, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClientErrorsMapToStableSanitizedCategories()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.GetPartyAsync("party-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new PartiesClientException(
                403,
                "Forbidden",
                "https://errors.example/forbidden",
                "Bearer token and payload for Jane Secret were denied by dapr sidecar",
                "corr-403"));
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.GetParty("party-1", CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("forbidden");
        result.Code.ShouldBe("parties-mcp-forbidden");
        result.CorrelationId.ShouldBe("corr-403");
        result.Message.ShouldNotContain("Bearer", Case.Insensitive);
        result.Message.ShouldNotContain("payload", Case.Insensitive);
        result.Message.ShouldNotContain("Jane Secret", Case.Insensitive);
        result.Message.ShouldNotContain("dapr", Case.Insensitive);
    }

    private static StubContextAccessor AuthenticatedContext()
        => new(new PartiesMcpRequestContext(TenantId, UserId, "Bearer safe-token"));

    private sealed class StubContextAccessor(PartiesMcpRequestContext? context) : IPartiesMcpRequestContextAccessor
    {
        public PartiesMcpRequestContext? Current => context;
    }
}
