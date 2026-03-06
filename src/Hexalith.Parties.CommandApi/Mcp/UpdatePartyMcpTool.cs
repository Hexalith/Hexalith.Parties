using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using ModelContextProtocol.Server;

namespace Hexalith.Parties.CommandApi.Mcp;

[McpServerToolType]
public static class UpdatePartyMcpTool
{
    [McpServerTool(Name = "update_party")]
    [Description("Updates an existing party using patch semantics — only specified fields are modified. Supports updating person/organization details, adding/updating/removing contact channels, and adding/removing identifiers. Missing IDs for new items are auto-generated.")]
    public static async Task<string> UpdatePartyAsync(
        [Description("The party ID to update (UUID)")] string partyId,
        IServiceProvider services,
        [Description("Updated person first name")] string? firstName = null,
        [Description("Updated person last name")] string? lastName = null,
        [Description("Updated person date of birth in ISO 8601 format (e.g., '1990-01-15')")] string? dateOfBirth = null,
        [Description("Updated name prefix (e.g., 'Mr.', 'Dr.')")] string? prefix = null,
        [Description("Updated name suffix (e.g., 'Jr.', 'III')")] string? suffix = null,
        [Description("Updated organization legal name")] string? legalName = null,
        [Description("Updated organization trading/brand name")] string? tradingName = null,
        [Description("Updated legal form (e.g., 'SAS', 'SARL')")] string? legalForm = null,
        [Description("Updated company registration number")] string? registrationNumber = null,
        [Description("New email address to add as contact channel")] string? addEmail = null,
        [Description("New phone number to add as contact channel")] string? addPhone = null,
        [Description("The ID of an existing contact channel to update (UUID)")] string? updateContactChannelId = null,
        [Description("Updated contact channel type: 'Email', 'Phone', 'PostalAddress', or 'SocialMedia'")] string? updateContactChannelType = null,
        [Description("Updated contact channel value")] string? updateContactChannelValue = null,
        [Description("Whether the updated contact channel should become preferred")] bool? updateContactChannelIsPreferred = null,
        [Description("Comma-separated list of contact channel IDs to remove")] string? removeContactChannelIds = null,
        [Description("New VAT number to add as identifier")] string? addVatNumber = null,
        [Description("Comma-separated list of identifier IDs to remove")] string? removeIdentifierIds = null,
        CancellationToken cancellationToken = default)
    {
        string? tenant = McpSessionContext.Tenant.Value;
        if (string.IsNullOrWhiteSpace(tenant))
        {
            throw new InvalidOperationException("Authentication required. No tenant context found in the request.");
        }

        if (!Guid.TryParse(partyId, out _))
        {
            throw new InvalidOperationException("Party ID is required and must be a valid UUID.");
        }

        // Check that at least one change is specified
        bool hasPersonFields = firstName is not null || lastName is not null || dateOfBirth is not null
            || prefix is not null || suffix is not null;
        bool hasOrgFields = legalName is not null || tradingName is not null || legalForm is not null
            || registrationNumber is not null;
        bool hasAddChannels = !string.IsNullOrWhiteSpace(addEmail) || !string.IsNullOrWhiteSpace(addPhone);
        bool hasUpdateChannels = updateContactChannelId is not null
            || updateContactChannelType is not null
            || updateContactChannelValue is not null
            || updateContactChannelIsPreferred is not null;
        bool hasRemoveChannels = !string.IsNullOrWhiteSpace(removeContactChannelIds);
        bool hasAddIdentifiers = !string.IsNullOrWhiteSpace(addVatNumber);
        bool hasRemoveIdentifiers = !string.IsNullOrWhiteSpace(removeIdentifierIds);

        if (!hasPersonFields && !hasOrgFields && !hasAddChannels && !hasUpdateChannels && !hasRemoveChannels
            && !hasAddIdentifiers && !hasRemoveIdentifiers)
        {
            throw new InvalidOperationException("No changes specified. Provide at least one field to update.");
        }

        // Query current party state for patch merge
        IActorProxyFactory actorProxyFactory = services.GetRequiredService<IActorProxyFactory>();
        var actorId = new ActorId($"{tenant}:party-detail:{partyId}");
        IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            actorId, nameof(PartyDetailProjectionActor));

        PartyDetail? currentParty = await proxy.GetDetailAsync().ConfigureAwait(false);
        if (currentParty is null)
        {
            throw new InvalidOperationException($"Party not found. No party exists with ID '{partyId}'.");
        }

        // Build PersonDetails with patch merge (only if person fields provided)
        PersonDetails? personDetails = null;
        if (hasPersonFields)
        {
            PersonDetails? current = currentParty.PersonDetails;
            DateTimeOffset? dob = dateOfBirth is not null ? ParseDateOfBirth(dateOfBirth) : current?.DateOfBirth;

            personDetails = new PersonDetails
            {
                FirstName = firstName ?? current?.FirstName ?? string.Empty,
                LastName = lastName ?? current?.LastName ?? string.Empty,
                DateOfBirth = dob,
                Prefix = prefix ?? current?.Prefix,
                Suffix = suffix ?? current?.Suffix,
            };
        }

        // Build OrganizationDetails with patch merge (only if org fields provided)
        OrganizationDetails? orgDetails = null;
        if (hasOrgFields)
        {
            OrganizationDetails? current = currentParty.OrganizationDetails;
            orgDetails = new OrganizationDetails
            {
                LegalName = legalName ?? current?.LegalName ?? string.Empty,
                TradingName = tradingName ?? current?.TradingName,
                LegalForm = legalForm ?? current?.LegalForm,
                RegistrationNumber = registrationNumber ?? current?.RegistrationNumber,
            };
        }

        // Build add contact channels
        List<AddContactChannel> addContactChannels = [];
        if (!string.IsNullOrWhiteSpace(addEmail))
        {
            addContactChannels.Add(new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = Guid.NewGuid().ToString(),
                Type = ContactChannelType.Email,
                Value = addEmail,
                IsPreferred = false,
            });
        }

        if (!string.IsNullOrWhiteSpace(addPhone))
        {
            addContactChannels.Add(new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = Guid.NewGuid().ToString(),
                Type = ContactChannelType.Phone,
                Value = addPhone,
                IsPreferred = false,
            });
        }

        List<UpdateContactChannel> updateContactChannels = BuildUpdateContactChannels(
            currentParty,
            updateContactChannelId,
            updateContactChannelType,
            updateContactChannelValue,
            updateContactChannelIsPreferred);

        // Parse remove contact channel IDs
        List<string> removeChannelIds = ParseAndValidateIds(
            removeContactChannelIds,
            "Invalid contact channel ID '{0}'. Must be a valid UUID.");

        // Build add identifiers
        List<AddIdentifier> addIdentifiers = [];
        if (!string.IsNullOrWhiteSpace(addVatNumber))
        {
            addIdentifiers.Add(new AddIdentifier
            {
                PartyId = partyId,
                IdentifierId = Guid.NewGuid().ToString(),
                Type = IdentifierType.VAT,
                Value = addVatNumber,
            });
        }

        // Parse remove identifier IDs
        List<string> removeIdentIds = ParseAndValidateIds(
            removeIdentifierIds,
            "Invalid identifier ID '{0}'. Must be a valid UUID.");

        // Construct UpdatePartyComposite with only populated fields (patch semantics)
        var command = new UpdatePartyComposite
        {
            PartyId = partyId,
            PersonDetails = personDetails,
            OrganizationDetails = orgDetails,
            AddContactChannels = addContactChannels,
            UpdateContactChannels = updateContactChannels,
            RemoveContactChannelIds = removeChannelIds,
            AddIdentifiers = addIdentifiers,
            RemoveIdentifierIds = removeIdentIds,
        };

        // Validate using FluentValidation
        IValidator<UpdatePartyComposite> validator = services.GetRequiredService<IValidator<UpdatePartyComposite>>();
        ValidationResult validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            string errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        // Dispatch command via ICommandRouter
        ICommandRouter commandRouter = services.GetRequiredService<ICommandRouter>();

        var submitCommand = new SubmitCommand(
            Tenant: tenant,
            Domain: "party",
            AggregateId: partyId,
            CommandType: nameof(UpdatePartyComposite),
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: Guid.NewGuid().ToString(),
            UserId: "mcp-agent");

        CommandProcessingResult result = await commandRouter
            .RouteCommandAsync(submitCommand, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted)
        {
            throw new InvalidOperationException($"Update failed: {result.ErrorMessage}");
        }

        // Query updated PartyDetail from projection (eventual consistency — return whatever projection has)
        PartyDetail? updatedDetail = await proxy.GetDetailAsync().ConfigureAwait(false);
        updatedDetail ??= currentParty;

        return JsonSerializer.Serialize(updatedDetail, McpSessionContext.JsonOptions);
    }

    private static List<UpdateContactChannel> BuildUpdateContactChannels(
        PartyDetail currentParty,
        string? updateContactChannelId,
        string? updateContactChannelType,
        string? updateContactChannelValue,
        bool? updateContactChannelIsPreferred)
    {
        bool hasUpdateInput = updateContactChannelId is not null
            || updateContactChannelType is not null
            || updateContactChannelValue is not null
            || updateContactChannelIsPreferred is not null;

        if (!hasUpdateInput)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(updateContactChannelId) || !Guid.TryParse(updateContactChannelId, out _))
        {
            throw new InvalidOperationException(
                "Updated contact channel ID is required and must be a valid UUID when modifying an existing contact channel.");
        }

        if (updateContactChannelType is null && updateContactChannelValue is null && updateContactChannelIsPreferred is null)
        {
            throw new InvalidOperationException(
                "At least one contact channel field must be provided when updating an existing contact channel.");
        }

        if (!currentParty.ContactChannels.Any(channel => string.Equals(channel.Id, updateContactChannelId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Contact channel '{updateContactChannelId}' not found.");
        }

        ContactChannelType? parsedType = ParseContactChannelType(updateContactChannelType);

        return
        [
            new UpdateContactChannel
            {
                PartyId = currentParty.Id,
                ContactChannelId = updateContactChannelId,
                Type = parsedType,
                Value = updateContactChannelValue,
                IsPreferred = updateContactChannelIsPreferred,
            },
        ];
    }

    private static ContactChannelType? ParseContactChannelType(string? contactChannelType)
    {
        if (string.IsNullOrWhiteSpace(contactChannelType))
        {
            return null;
        }

        if (Enum.TryParse(contactChannelType, ignoreCase: true, out ContactChannelType parsedType))
        {
            return parsedType;
        }

        throw new InvalidOperationException(
            $"Invalid contact channel type '{contactChannelType}'. Must be one of: Email, Phone, PostalAddress, SocialMedia.");
    }

    private static DateTimeOffset? ParseDateOfBirth(string? dateOfBirth)
    {
        if (string.IsNullOrWhiteSpace(dateOfBirth))
        {
            return null;
        }

        string[] supportedFormats =
        [
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
            "O",
        ];

        if (DateTimeOffset.TryParseExact(
            dateOfBirth,
            supportedFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out DateTimeOffset parsedDob))
        {
            return parsedDob;
        }

        throw new InvalidOperationException(
            "Date of birth must be a valid ISO 8601 date or date-time (for example '1990-01-15').");
    }

    private static List<string> ParseAndValidateIds(string? commaSeparatedIds, string errorMessageFormat)
    {
        if (string.IsNullOrWhiteSpace(commaSeparatedIds))
        {
            return [];
        }

        List<string> ids = [];
        foreach (string raw in commaSeparatedIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(raw, out _))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, errorMessageFormat, raw));
            }

            ids.Add(raw);
        }

        return ids;
    }
}
