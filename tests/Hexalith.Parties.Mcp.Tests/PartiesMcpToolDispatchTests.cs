using System.Text.Json;

using Hexalith.Commons.UniqueIds;
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
    public async Task ToolsFailClosedBeforeClientAccessWhenTenantOrUserContextIsMissing()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        var tools = new PartiesMcpTools(commandClient, queryClient, new StubContextAccessor(null));

        PartiesMcpToolResult find = await tools.FindParties("ada", cancellationToken: CancellationToken.None);
        PartiesMcpToolResult create = await tools.CreateParty(
            partyType: "person",
            familyName: "Lovelace",
            cancellationToken: CancellationToken.None);
        PartiesMcpToolResult update = await tools.UpdateParty(
            partyId: "party-1",
            addEmail: "ada@example.test",
            cancellationToken: CancellationToken.None);
        PartiesMcpToolResult delete = await tools.DeleteParty("party-1", CancellationToken.None);

        find.Category.ShouldBe("missing_context");
        create.Category.ShouldBe("missing_context");
        update.Category.ShouldBe("missing_context");
        delete.Category.ShouldBe("missing_context");
        await queryClient.DidNotReceiveWithAnyArgs().SearchPartiesAsync(default!, default, default, default);
        await queryClient.DidNotReceiveWithAnyArgs().ListPartiesAsync(default, default, default, default, default, default, default, default, default);
        await queryClient.DidNotReceiveWithAnyArgs().GetPartyAsync(default!, default);
        await commandClient.DidNotReceiveWithAnyArgs().CreatePartyCompositeWithResultAsync(default!, default);
        await commandClient.DidNotReceiveWithAnyArgs().UpdatePartyCompositeWithResultAsync(default!, default!, default);
        await commandClient.DidNotReceiveWithAnyArgs().DeactivatePartyWithResultAsync(default!, default);
    }

    [Fact]
    public async Task GetPartyReturnsCompleteDetailWithInactiveAndFreshnessMetadata()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.GetPartyAsync("party-1", Arg.Any<CancellationToken>())
            .Returns(new PartyDetail
            {
                Id = "party-1",
                Type = PartyType.Person,
                IsActive = false,
                DisplayName = "Ada Lovelace",
                SortName = "Lovelace, Ada",
                CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                LastModifiedAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
                Freshness = ProjectionFreshnessMetadata.Create(
                    ProjectionFreshnessStatus.Stale,
                    ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable),
            });
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.GetParty("party-1", CancellationToken.None);

        result.Status.ShouldBe("succeeded");
        JsonElement data = result.Data.ShouldBeOfType<JsonElement>();
        data.GetProperty("id").GetString().ShouldBe("party-1");
        data.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        data.GetProperty("isErased").GetBoolean().ShouldBeFalse();
        data.GetProperty("freshness").GetProperty("status").GetString().ShouldBe("Stale");
        data.GetProperty("freshness").GetProperty("warningCodes")[0].GetString().ShouldBe(ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable);
    }

    [Fact]
    public async Task GetPartyRejectsMalformedPartyIdBeforeGatewayCall()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.GetParty("party id with spaces", CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-validation-failed");
        result.Message.ShouldContain("partyId");
        result.Message.ShouldNotContain("party id with spaces");
        await queryClient.DidNotReceiveWithAnyArgs().GetPartyAsync(default!, default);
    }

    [Fact]
    public async Task GetPartyTreatsErasedPartyAsNotFoundWithoutErasureDetails()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.GetPartyAsync("party-erased", Arg.Any<CancellationToken>())
            .Returns(new PartyDetail
            {
                Id = "party-erased",
                Type = PartyType.Person,
                IsActive = false,
                IsErased = true,
                DisplayName = "Erased",
                SortName = "Erased",
                ErasedAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
            });
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.GetParty("party-erased", CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("not_found");
        result.Code.ShouldBe("parties-mcp-party-erased");
        result.Message.ShouldBe("The requested Parties resource was not found.");
        result.Message.ShouldNotContain("erased", Case.Insensitive);
        result.Data.ShouldBeNull();
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
                command != null && command.PartyId == "party-1"
                && command.Type == PartyType.Person
                && command.PersonDetails!.FirstName == "Ada"
                && command.PersonDetails.LastName == "Lovelace"
                && command.ContactChannels.Count == 1
                && command.Identifiers.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartyWithoutCallerSuppliedIdsGeneratesSortableUniqueIds()
    {
        CreatePartyComposite? captured = null;
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.CreatePartyCompositeWithResultAsync(
                Arg.Do<CreatePartyComposite>(command => captured = command),
                Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-create", null));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            partyType: "person",
            givenName: "Ada",
            familyName: "Lovelace",
            email: "ada@example.test",
            identifierType: "TaxId",
            identifierValue: "TAX-123",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        captured.ShouldNotBeNull();
        ShouldBeSortableUniqueId(captured.PartyId);
        ShouldBeSortableUniqueId(captured.ContactChannels.Single().ContactChannelId);
        ShouldBeSortableUniqueId(captured.Identifiers.Single().IdentifierId);
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
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
            },
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "contact-1",
                    Type = ContactChannelType.Email,
                    Value = "ada@example.test",
                    IsPreferred = true,
                },
            ],
            Identifiers =
            [
                new PartyIdentifier
                {
                    Id = "identifier-1",
                    Type = IdentifierType.TaxId,
                    Value = "TAX-123",
                },
            ],
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
        data.GetProperty("personDetails").GetProperty("firstName").GetString().ShouldBe("Ada");
        data.GetProperty("contactChannels")[0].GetProperty("value").GetString().ShouldBe("ada@example.test");
        data.GetProperty("identifiers")[0].GetProperty("value").GetString().ShouldBe("TAX-123");
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
                command != null && command.Type == PartyType.Person
                && command.PersonDetails!.FirstName == string.Empty
                && command.PersonDetails.LastName == "Lovelace"
                && command.PersonDetails.DateOfBirth == new DateTimeOffset(1815, 12, 10, 0, 0, 0, TimeSpan.Zero)
                && command.PersonDetails.Prefix == "Dr."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartyBuildsOrganizationCompositeWithOptionalContactsAndIdentifierDefaults()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.CreatePartyCompositeWithResultAsync(Arg.Any<CreatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-create", null));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            legalName: "Hexalith SAS",
            tradingName: "Hexalith",
            email: "contact@hexalith.example",
            phone: "+33123456789",
            vatNumber: "FR123456789",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).CreatePartyCompositeWithResultAsync(
            Arg.Is<CreatePartyComposite>(command =>
                command != null && command.Type == PartyType.Organization
                && command.OrganizationDetails!.LegalName == "Hexalith SAS"
                && command.OrganizationDetails.TradingName == "Hexalith"
                && command.ContactChannels.Count == 2
                && command.ContactChannels[0].Type == ContactChannelType.Email
                && command.ContactChannels[0].IsPreferred
                && command.ContactChannels[1].Type == ContactChannelType.Phone
                && !command.ContactChannels[1].IsPreferred
                && command.Identifiers.Count == 1
                && command.Identifiers[0].Type == IdentifierType.VAT
                && command.Identifiers[0].Value == "FR123456789"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartyNormalizesDuplicateIdentifierAliasesToSingleSubOperation()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.CreatePartyCompositeWithResultAsync(Arg.Any<CreatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-create", null));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            partyType: "organization",
            legalName: "Hexalith SAS",
            vatNumber: "FR123456789",
            identifierType: "VAT",
            identifierValue: "FR123456789",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).CreatePartyCompositeWithResultAsync(
            Arg.Is<CreatePartyComposite>(command =>
                command != null && command.Identifiers.Count == 1
                && command.Identifiers[0].Type == IdentifierType.VAT
                && command.Identifiers[0].Value == "FR123456789"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartyRejectsMissingRequiredDetailsBeforeCallingClient()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-validation-failed");
        result.Message.ShouldContain("party type");
        await commandClient.DidNotReceiveWithAnyArgs().CreatePartyCompositeWithResultAsync(default!, default);
    }

    [Theory]
    [InlineData("party/unsafe")]
    [InlineData("tenant-a:parties:party-1")]
    public async Task CreatePartyRejectsUnsafeCallerSuppliedPartyIdBeforeCallingClient(string partyId)
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            partyId: partyId,
            partyType: "person",
            familyName: "Lovelace",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Message.ShouldContain("partyId");
        await commandClient.DidNotReceiveWithAnyArgs().CreatePartyCompositeWithResultAsync(default!, default);
    }

    [Fact]
    public async Task CreatePartyRejectsOversizedPayloadBeforeCallingClient()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            partyType: "organization",
            legalName: new string('A', 513),
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-payload-too-large");
        result.Message.ShouldNotContain("AAAA", Case.Insensitive);
        await commandClient.DidNotReceiveWithAnyArgs().CreatePartyCompositeWithResultAsync(default!, default);
    }

    [Fact]
    public async Task CreatePartyGatewayValidationFailureReturnsSafeFailureWithoutPartialSuccess()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.CreatePartyCompositeWithResultAsync(Arg.Any<CreatePartyComposite>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PartiesClientException(
                422,
                "Validation failed",
                "https://errors.example/validation",
                "Payload contained duplicate channel for Jane Secret",
                "corr-422"));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.CreateParty(
            partyType: "person",
            familyName: "Lovelace",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-validation-failed");
        result.CorrelationId.ShouldBe("corr-422");
        result.Data.ShouldBeNull();
        result.Message.ShouldNotContain("duplicate", Case.Insensitive);
        result.Message.ShouldNotContain("Jane Secret", Case.Insensitive);
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
                command != null && command.PartyId == "route-party"
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
                command != null && command.PersonDetails!.FirstName == "Augusta"
                && command.PersonDetails.LastName == "Lovelace"
                && command.PersonDetails.Prefix == "Countess"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyMergesPartialOrganizationPatchThroughQueryClient()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        commandClient.UpdatePartyCompositeWithResultAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-update", null));
        queryClient.GetPartyAsync("party-1", Arg.Any<CancellationToken>())
            .Returns(new PartyDetail
            {
                Id = "party-1",
                Type = PartyType.Organization,
                IsActive = true,
                DisplayName = "Hexalith SAS",
                SortName = "Hexalith SAS",
                OrganizationDetails = new OrganizationDetails
                {
                    LegalName = "Hexalith SAS",
                    TradingName = "Hexalith",
                    LegalForm = "SAS",
                    RegistrationNumber = "RCS-1",
                },
            });
        var tools = new PartiesMcpTools(commandClient, queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            tradingName: "Hexalith Labs",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).UpdatePartyCompositeWithResultAsync(
            "party-1",
            Arg.Is<UpdatePartyComposite>(command =>
                command != null && command.OrganizationDetails!.LegalName == "Hexalith SAS"
                && command.OrganizationDetails.TradingName == "Hexalith Labs"
                && command.OrganizationDetails.LegalForm == "SAS"
                && command.OrganizationDetails.RegistrationNumber == "RCS-1"),
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
                command != null && command.RemoveContactChannelIds.Count == 2
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
                command != null && command.AddContactChannels.Single().Type == ContactChannelType.Email
                && command.UpdateContactChannels.Single().ContactChannelId == "contact-1"
                && command.UpdateContactChannels.Single().Type == ContactChannelType.Phone
                && command.RemoveContactChannelIds.Single() == "contact-old"
                && command.AddIdentifiers.Single().Type == IdentifierType.VAT
                && command.RemoveIdentifierIds.Single() == "identifier-old"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyPreservesLegacyXFormatSingleChildIds()
    {
        const string updateContactId = "{0x12345678,0x1234,0x5678,{0x90,0xab,0xcd,0xef,0x12,0x34,0x56,0x78}}";
        const string removeContactId = "{0x22345678,0x1234,0x5678,{0x90,0xab,0xcd,0xef,0x12,0x34,0x56,0x78}}";
        const string removeIdentifierId = "{0x32345678,0x1234,0x5678,{0x90,0xab,0xcd,0xef,0x12,0x34,0x56,0x78}}";
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
                ContactChannels =
                [
                    new ContactChannel
                    {
                        Id = updateContactId,
                        Type = ContactChannelType.Email,
                        Value = "old@example.test",
                    },
                ],
            });
        var tools = new PartiesMcpTools(commandClient, queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            updateContactChannelId: updateContactId,
            updateContactChannelValue: "new@example.test",
            removeContactChannelId: removeContactId,
            removeIdentifierId: removeIdentifierId,
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).UpdatePartyCompositeWithResultAsync(
            "party-1",
            Arg.Is<UpdatePartyComposite>(command =>
                command != null
                && command.UpdateContactChannels.Single().ContactChannelId == updateContactId
                && command.RemoveContactChannelIds.Single() == removeContactId
                && command.RemoveIdentifierIds.Single() == removeIdentifierId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyRejectsUnsafeChildIdsBeforeClientAccess()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        var tools = new PartiesMcpTools(commandClient, queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            updateContactChannelId: "contact/unsafe",
            updateContactChannelValue: "+33123456789",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-validation-failed");
        result.Message.ShouldContain("updateContactChannelId");
        result.Message.ShouldNotContain("contact/unsafe");
        await queryClient.DidNotReceiveWithAnyArgs().GetPartyAsync(default!, default);
        await commandClient.DidNotReceiveWithAnyArgs().UpdatePartyCompositeWithResultAsync(default!, default!, default);
    }

    [Fact]
    public async Task UpdatePartyRejectsUnsafeRemovalIdsBeforeClientAccess()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        var tools = new PartiesMcpTools(commandClient, queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            removeContactChannelIds: "contact-1, contact/unsafe",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-validation-failed");
        result.Message.ShouldContain("removeContactChannelIds");
        result.Message.ShouldNotContain("contact/unsafe");
        await queryClient.DidNotReceiveWithAnyArgs().GetPartyAsync(default!, default);
        await commandClient.DidNotReceiveWithAnyArgs().UpdatePartyCompositeWithResultAsync(default!, default!, default);
    }

    [Fact]
    public async Task UpdatePartyNormalizesIdentifierAliasesToSingleSubOperation()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.UpdatePartyCompositeWithResultAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-update", null));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            addVatNumber: "FR123",
            addIdentifierType: "VAT",
            addIdentifierValue: "FR123",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("accepted");
        await commandClient.Received(1).UpdatePartyCompositeWithResultAsync(
            "party-1",
            Arg.Is<UpdatePartyComposite>(command =>
                command != null && command.AddIdentifiers.Count == 1
                && command.AddIdentifiers[0].Type == IdentifierType.VAT
                && command.AddIdentifiers[0].Value == "FR123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyReturnsSucceededWithCompleteUpdatedDetailWhenCommandPayloadIsAvailable()
    {
        var updatedDetail = new PartyDetail
        {
            Id = "party-1",
            Type = PartyType.Organization,
            IsActive = true,
            DisplayName = "Hexalith Labs",
            SortName = "Hexalith Labs",
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Hexalith SAS",
                TradingName = "Hexalith Labs",
            },
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "contact-1",
                    Type = ContactChannelType.Email,
                    Value = "contact@hexalith.example",
                    IsPreferred = true,
                },
            ],
            Identifiers =
            [
                new PartyIdentifier
                {
                    Id = "identifier-1",
                    Type = IdentifierType.VAT,
                    Value = "FR123",
                },
            ],
        };
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.UpdatePartyCompositeWithResultAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-update", updatedDetail));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            addEmail: "contact@hexalith.example",
            addVatNumber: "FR123",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("succeeded");
        result.CorrelationId.ShouldBe("corr-update");
        JsonElement data = result.Data.ShouldBeOfType<JsonElement>();
        data.GetProperty("id").GetString().ShouldBe("party-1");
        data.GetProperty("displayName").GetString().ShouldBe("Hexalith Labs");
        data.GetProperty("organizationDetails").GetProperty("tradingName").GetString().ShouldBe("Hexalith Labs");
        data.GetProperty("contactChannels")[0].GetProperty("value").GetString().ShouldBe("contact@hexalith.example");
        data.GetProperty("identifiers")[0].GetProperty("value").GetString().ShouldBe("FR123");
    }

    [Fact]
    public async Task UpdatePartyRejectsInvalidCompositePatchEvenWhenLifecyclePatchIsPresent()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            addIdentifierType: "UnknownIdentifierType",
            addIdentifierValue: "FR123",
            active: false,
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Message.ShouldContain("update fields");
        await commandClient.DidNotReceiveWithAnyArgs().UpdatePartyCompositeWithResultAsync(default!, default!, default);
        await commandClient.DidNotReceiveWithAnyArgs().DeactivatePartyWithResultAsync(default!, default);
    }

    [Fact]
    public async Task UpdatePartyRejectsOversizedPayloadBeforeCallingClient()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            legalName: new string('A', 513),
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-payload-too-large");
        result.Message.ShouldNotContain("AAAA", Case.Insensitive);
        await commandClient.DidNotReceiveWithAnyArgs().UpdatePartyCompositeWithResultAsync(default!, default!, default);
    }

    [Fact]
    public async Task UpdatePartyGatewayValidationFailureReturnsSafeFailureWithoutPartialSuccess()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        commandClient.UpdatePartyCompositeWithResultAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PartiesClientException(
                422,
                "Validation failed",
                "https://errors.example/validation",
                "Payload contained conflicting contact change for Jane Secret",
                "corr-422"));
        var tools = new PartiesMcpTools(commandClient, Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.UpdateParty(
            partyId: "party-1",
            addEmail: "new@example.test",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-validation-failed");
        result.CorrelationId.ShouldBe("corr-422");
        result.Data.ShouldBeNull();
        result.Message.ShouldNotContain("conflicting", Case.Insensitive);
        result.Message.ShouldNotContain("Jane Secret", Case.Insensitive);
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
        JsonElement data = result.Data.ShouldBeOfType<JsonElement>();
        data.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        data.GetProperty("idempotent").GetBoolean().ShouldBeTrue();
        data.GetProperty("operation").GetString().ShouldBe("soft-deactivation");
        data.GetProperty("gdprErasurePerformed").GetBoolean().ShouldBeFalse();
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
        data.GetProperty("operation").GetString().ShouldBe("soft-deactivation");
        data.GetProperty("gdprErasurePerformed").GetBoolean().ShouldBeFalse();
        JsonElement partyDetail = data.GetProperty("partyDetail");
        partyDetail.GetProperty("id").GetString().ShouldBe("party-1");
        partyDetail.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task DeletePartyReturnsSoftDeactivationConfirmationWhenGatewayPayloadIsUnavailable()
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
            .Returns(new PartiesCommandResult<PartyDetail>("corr-delete", null));
        var tools = new PartiesMcpTools(commandClient, queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.DeleteParty("party-1", CancellationToken.None);

        result.Status.ShouldBe("accepted");
        result.CorrelationId.ShouldBe("corr-delete");
        JsonElement data = result.Data.ShouldBeOfType<JsonElement>();
        data.GetProperty("partyId").GetString().ShouldBe("party-1");
        data.GetProperty("requestedState").GetString().ShouldBe("inactive");
        data.GetProperty("operation").GetString().ShouldBe("soft-deactivation");
        data.GetProperty("gdprErasurePerformed").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task DeletePartyRejectsMissingOrMalformedPartyIdBeforeQuerying()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.DeleteParty("party/unsafe", CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Message.ShouldContain("partyId");
        await queryClient.DidNotReceiveWithAnyArgs().GetPartyAsync(default!, default);
    }

    [Fact]
    public async Task DeletePartyFailsClosedWhenContextIsMissingBeforeQuerying()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, new StubContextAccessor(null));

        PartiesMcpToolResult result = await tools.DeleteParty("party-1", CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("missing_context");
        await queryClient.DidNotReceiveWithAnyArgs().GetPartyAsync(default!, default);
    }

    [Fact]
    public async Task DeletePartyMapsNotFoundWithoutLeakingCrossTenantDetails()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.GetPartyAsync("party-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new PartiesClientException(
                404,
                "Not Found",
                "https://errors.example/not-found",
                "Tenant tenant-a cannot see party Jane Secret",
                "corr-404"));
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.DeleteParty("party-1", CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("not_found");
        result.Code.ShouldBe("parties-mcp-not-found");
        result.CorrelationId.ShouldBe("corr-404");
        result.Data.ShouldBeNull();
        result.Message.ShouldNotContain("tenant-a", Case.Insensitive);
        result.Message.ShouldNotContain("Jane Secret", Case.Insensitive);
    }

    [Fact]
    public async Task DeletePartyRetryForAlreadyInactivePartyRemainsStableAndSideEffectFree()
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

        PartiesMcpToolResult first = await tools.DeleteParty("party-1", CancellationToken.None);
        PartiesMcpToolResult second = await tools.DeleteParty("party-1", CancellationToken.None);

        first.Status.ShouldBe("succeeded");
        second.Status.ShouldBe("succeeded");
        first.Code.ShouldBe("parties-mcp-delete-idempotent");
        second.Code.ShouldBe("parties-mcp-delete-idempotent");
        first.Data.ShouldBeOfType<JsonElement>().GetRawText().ShouldBe(second.Data.ShouldBeOfType<JsonElement>().GetRawText());
        await commandClient.DidNotReceiveWithAnyArgs().DeactivatePartyWithResultAsync(default!, default);
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

        (await tools.FindParties("ada", 2, 10, null, null, cancellationToken: CancellationToken.None)).Status.ShouldBe("succeeded");
        (await tools.FindParties(null, 1, 20, "organization", true, cancellationToken: CancellationToken.None)).Status.ShouldBe("succeeded");

        await queryClient.Received(1).SearchPartiesAsync("ada", 2, 10, Arg.Any<CancellationToken>());
        await queryClient.Received(1).ListPartiesAsync(1, 20, PartyType.Organization, true, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindPartiesListModeParsesCreatedAndModifiedDateFilters()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        DateTimeOffset createdAfter = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        DateTimeOffset createdBefore = DateTimeOffset.Parse("2026-01-31T23:59:59Z");
        DateTimeOffset modifiedAfter = DateTimeOffset.Parse("2026-02-01T00:00:00Z");
        DateTimeOffset modifiedBefore = DateTimeOffset.Parse("2026-02-28T23:59:59Z");
        queryClient.ListPartiesAsync(1, 25, null, null, createdAfter, createdBefore, modifiedAfter, modifiedBefore, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<PartyIndexEntry> { Items = [], Page = 1, PageSize = 25 });
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.FindParties(
            pageSize: 25,
            createdAfter: "2026-01-01T00:00:00Z",
            createdBefore: "2026-01-31T23:59:59Z",
            modifiedAfter: "2026-02-01T00:00:00Z",
            modifiedBefore: "2026-02-28T23:59:59Z",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("succeeded");
        await queryClient.Received(1).ListPartiesAsync(1, 25, null, null, createdAfter, createdBefore, modifiedAfter, modifiedBefore, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindPartiesInvalidDateFilterReturnsBoundedValidationError()
    {
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), Substitute.For<IPartiesQueryClient>(), AuthenticatedContext());

        PartiesMcpToolResult result = await tools.FindParties(
            createdAfter: "not-a-date",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("validation_failed");
        result.Code.ShouldBe("parties-mcp-validation-failed");
        result.Message.ShouldContain("createdAfter");
        result.Message.ShouldNotContain("not-a-date");
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
    public async Task TimeoutAndDownstreamFailuresMapToStableSafeErrors()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.GetPartyAsync("party-timeout", Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Jane Secret timeout payload"));
        queryClient.GetPartyAsync("party-downstream", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Bearer token for Jane Secret failed"));
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult timeout = await tools.GetParty("party-timeout", CancellationToken.None);
        PartiesMcpToolResult downstream = await tools.GetParty("party-downstream", CancellationToken.None);

        timeout.Status.ShouldBe("failed");
        timeout.Category.ShouldBe("timeout");
        timeout.Code.ShouldBe("parties-mcp-timeout");
        timeout.Message.ShouldNotContain("Jane Secret", Case.Insensitive);
        downstream.Status.ShouldBe("failed");
        downstream.Category.ShouldBe("downstream_failed");
        downstream.Code.ShouldBe("parties-mcp-downstream-failed");
        downstream.Message.ShouldNotContain("Bearer", Case.Insensitive);
        downstream.Message.ShouldNotContain("Jane Secret", Case.Insensitive);
    }

    [Fact]
    public async Task CanceledOperationMapsToStableCanceledError()
    {
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.GetPartyAsync("party-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Jane Secret canceled"));
        var tools = new PartiesMcpTools(Substitute.For<IPartiesCommandClient>(), queryClient, AuthenticatedContext());

        PartiesMcpToolResult result = await tools.GetParty("party-1", CancellationToken.None);

        result.Status.ShouldBe("failed");
        result.Category.ShouldBe("canceled");
        result.Code.ShouldBe("parties-mcp-canceled");
        result.Message.ShouldNotContain("Jane Secret", Case.Insensitive);
    }

    [Fact]
    public async Task HealthyToolsCompleteWithinMvpInProcessLatencyBudget()
    {
        IPartiesCommandClient commandClient = Substitute.For<IPartiesCommandClient>();
        IPartiesQueryClient queryClient = Substitute.For<IPartiesQueryClient>();
        queryClient.GetPartyAsync("party-1", Arg.Any<CancellationToken>())
            .Returns(new PartyDetail
            {
                Id = "party-1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Lovelace",
                SortName = "Lovelace, Ada",
                Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
            });
        queryClient.ListPartiesAsync(1, 20, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<PartyIndexEntry> { Items = [], Page = 1, PageSize = 20 });
        commandClient.CreatePartyCompositeWithResultAsync(Arg.Any<CreatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-create", new PartyDetail
            {
                Id = "party-created",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Lovelace",
                SortName = "Lovelace, Ada",
            }));
        commandClient.UpdatePartyCompositeWithResultAsync(Arg.Any<string>(), Arg.Any<UpdatePartyComposite>(), Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-update", new PartyDetail
            {
                Id = "party-1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Byron",
                SortName = "Byron, Ada",
            }));
        commandClient.DeactivatePartyWithResultAsync("party-1", Arg.Any<CancellationToken>())
            .Returns(new PartiesCommandResult<PartyDetail>("corr-delete", new PartyDetail
            {
                Id = "party-1",
                Type = PartyType.Person,
                IsActive = false,
                DisplayName = "Ada Lovelace",
                SortName = "Lovelace, Ada",
            }));
        var tools = new PartiesMcpTools(commandClient, queryClient, AuthenticatedContext());

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        PartiesMcpToolResult get = await tools.GetParty("party-1", CancellationToken.None);
        PartiesMcpToolResult find = await tools.FindParties(cancellationToken: CancellationToken.None);
        PartiesMcpToolResult create = await tools.CreateParty(partyType: "person", familyName: "Lovelace", cancellationToken: CancellationToken.None);
        PartiesMcpToolResult update = await tools.UpdateParty("party-1", addEmail: "ada@example.test", cancellationToken: CancellationToken.None);
        PartiesMcpToolResult delete = await tools.DeleteParty("party-1", CancellationToken.None);
        stopwatch.Stop();

        get.Status.ShouldBe("succeeded");
        find.Status.ShouldBe("succeeded");
        create.Status.ShouldBe("succeeded");
        update.Status.ShouldBe("succeeded");
        delete.Status.ShouldBe("succeeded");
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
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

    private static void ShouldBeSortableUniqueId(string id)
    {
        DateTimeOffset timestamp = UniqueIdHelper.ExtractTimestamp(id);
        timestamp.ShouldBeGreaterThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(-1));
        timestamp.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
        Guid.TryParse(id, out _).ShouldBeFalse();
    }
}
