using System.Text.Json;

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
        commandClient.CreatePartyCompositeWithResultAsync(Arg.Any<CreatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-create", null));
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

        await commandClient.Received(1).CreatePartyCompositeWithResultAsync(
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
    public async Task CreatePartyReturnsSucceededWithUpdatedDetailWhenCommandPayloadIsAvailable()
    {
        var updatedDetail = new PartyDetail
        {
            Id = "party-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
        };
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.CreatePartyCompositeWithResultAsync(Arg.Any<CreatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-create", updatedDetail));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            partyId: "party-1",
            partyType: "person",
            givenName: "Ada",
            familyName: "Lovelace",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("succeeded");
        result.CorrelationId.ShouldBe("corr-create");
        JsonElement data = result.Data.ShouldBeOfType<JsonElement>();
        data.GetProperty("id").GetString().ShouldBe("party-1");
        data.GetProperty("displayName").GetString().ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task CreatePartyPreservesPrePivotPersonAliasesAndPartialPersonInput()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.CreatePartyCompositeWithResultAsync(Arg.Any<CreatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-create", null));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            partyType: "person",
            firstName: null,
            lastName: "Lovelace",
            dateOfBirth: "1815-12-10",
            prefix: "Dr.",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).CreatePartyCompositeWithResultAsync(
            Arg.Is<CreatePartyComposite>(command =>
                command.Type == PartyType.Person
                && command.PersonDetails!.FirstName == string.Empty
                && command.PersonDetails.LastName == "Lovelace"
                && command.PersonDetails.DateOfBirth == new DateTimeOffset(1815, 12, 10, 0, 0, 0, TimeSpan.Zero)
                && command.PersonDetails.Prefix == "Dr."),
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
        await commandClient.DidNotReceiveWithAnyArgs().UpdatePartyCompositeWithResultAsync(default!, default!, default);
    }

    [Fact]
    public async Task UpdatePartyUsesRoutePartyIdAsAuthoritativeCompositeCommandId()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.UpdatePartyCompositeWithResultAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-update", null));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "route-party",
            givenName: "Grace",
            familyName: "Hopper",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).UpdatePartyCompositeWithResultAsync(
            "route-party",
            Arg.Is<UpdatePartyComposite>(command =>
                command.PartyId == "route-party"
                && command.PersonDetails!.FirstName == "Grace"
                && command.PersonDetails.LastName == "Hopper"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyMergesPartialPersonPatchThroughQueryClient()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        commandClient.UpdatePartyCompositeWithResultAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-update", null));
        queryClient.GetPartyAsync("party-1", Arg.Any<CancellationToken>())
            .Returns(new PartyDetail
            {
                Id = "party-1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Lovelace",
                SortName = "Lovelace, Ada",
                PersonDetails = new PersonDetails
                {
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    Prefix = "Countess",
                },
            });
        var tools = new PartiesMcpTools(commandClient, queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            firstName: "Augusta",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).UpdatePartyCompositeWithResultAsync(
            "party-1",
            Arg.Is<UpdatePartyComposite>(command =>
                command.PersonDetails!.FirstName == "Augusta"
                && command.PersonDetails.LastName == "Lovelace"
                && command.PersonDetails.Prefix == "Countess"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyHandlesLifecycleWithDetailsAndCommaSeparatedRemovals()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.UpdatePartyCompositeWithResultAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-update", null));
        commandClient.DeactivatePartyWithResultAsync("party-1", Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-deactivate", null));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            addEmail: "new@example.test",
            removeContactChannelIds: "contact-1, contact-2",
            removeIdentifierIds: "identifier-1, identifier-2",
            active: false,
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).UpdatePartyCompositeWithResultAsync(
            "party-1",
            Arg.Is<UpdatePartyComposite>(command =>
                command.RemoveContactChannelIds.Count == 2
                && command.RemoveContactChannelIds[0] == "contact-1"
                && command.RemoveContactChannelIds[1] == "contact-2"
                && command.RemoveIdentifierIds.Count == 2
                && command.RemoveIdentifierIds[0] == "identifier-1"
                && command.RemoveIdentifierIds[1] == "identifier-2"),
            Arg.Any<CancellationToken>());
        await commandClient.Received(1).DeactivatePartyWithResultAsync("party-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyPreservesContactAndIdentifierPatchOperations()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.UpdatePartyCompositeWithResultAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-update", null));
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
        await commandClient.Received(1).UpdatePartyCompositeWithResultAsync(
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
        await commandClient.DidNotReceiveWithAnyArgs().DeactivatePartyWithResultAsync(default!, default);
    }

    [Fact]
    public async Task DeletePartyReturnsSucceededWithUpdatedDetailWhenDeactivatePayloadIsAvailable()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        queryClient.GetPartyAsync("party-1", Arg.Any<CancellationToken>())
            .Returns(new PartyDetail
            {
                Id = "party-1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Active Person",
                SortName = "Person Active",
            });
        commandClient.DeactivatePartyWithResultAsync("party-1", Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>(
                "corr-delete",
                new PartyDetail
                {
                    Id = "party-1",
                    Type = PartyType.Person,
                    IsActive = false,
                    DisplayName = "Active Person",
                    SortName = "Person Active",
                }));
        var tools = new PartiesMcpTools(commandClient, queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.DeleteParty("party-1", CancellationToken.None);

        result.Status.ShouldBe("succeeded");
        result.CorrelationId.ShouldBe("corr-delete");
        JsonElement data = result.Data.ShouldBeOfType<JsonElement>();
        data.GetProperty("id").GetString().ShouldBe("party-1");
        data.GetProperty("isActive").GetBoolean().ShouldBeFalse();
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
    public async Task FindPartiesAppliesTypeAndActiveFiltersToSearchResults()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.SearchPartiesAsync("ada", 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<PartySearchResult>
            {
                Items =
                [
                    SearchResult("person-active", PartyType.Person, true),
                    SearchResult("org-active", PartyType.Organization, true),
                    SearchResult("org-inactive", PartyType.Organization, false),
                ],
                Page = 1,
                PageSize = 20,
                TotalCount = 3,
                TotalPages = 1,
            });
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.FindParties("ada", type: "organization", active: true, cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("succeeded");
        JsonElement data = result.Data.ShouldBeOfType<JsonElement>();
        JsonElement items = data.GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("party").GetProperty("id").GetString().ShouldBe("org-active");
        data.GetProperty("totalCount").GetInt32().ShouldBe(1);
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

    [Fact]
    public async Task CreatePartyReturnsAcceptedWhenGatewayPayloadIsMalformedOrUnsupported()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.CreatePartyCompositeWithResultAsync(Arg.Any<CreatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-malformed", null));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            partyId: "party-malformed",
            partyType: "person",
            givenName: "Ada",
            familyName: "Lovelace",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        result.CorrelationId.ShouldBe("corr-malformed");
        result.Data.ShouldBeNull();
    }

    private static StubContextAccessor AuthenticatedContext()
        => new(new PartiesMcpRequestContext(TenantId, UserId, "Bearer safe-token"));

    private static PartySearchResult SearchResult(string id, PartyType type, bool active)
        => new()
        {
            Party = new PartyIndexEntry
            {
                Id = id,
                Type = type,
                IsActive = active,
                DisplayName = id,
            },
            Matches = [],
            RelevanceScore = 1,
        };

    private sealed class StubContextAccessor(PartiesMcpRequestContext? context) : IPartiesMcpRequestContextAccessor
    {
        public PartiesMcpRequestContext? Current => context;
    }
}
