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
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using ModelContextProtocol.Server;

namespace Hexalith.Parties.Mcp;

[McpServerToolType]
public static class CreatePartyMcpTool
{
    [McpServerTool(Name = "create_party")]
    [Description("Creates a new party (person or organization) with optional contact channels and identifiers. Accepts forgiving input — missing IDs are auto-generated, partial details are accepted.")]
    public static async Task<string> CreatePartyAsync(
        [Description("The party type: 'Person' or 'Organization'")] string? type,
        IServiceProvider services,
        [Description("Person's first name")] string? firstName = null,
        [Description("Person's last name (required for Person type)")] string? lastName = null,
        [Description("Person's date of birth in ISO 8601 format (e.g., '1990-01-15')")] string? dateOfBirth = null,
        [Description("Name prefix (e.g., 'Mr.', 'Dr.')")] string? prefix = null,
        [Description("Name suffix (e.g., 'Jr.', 'III')")] string? suffix = null,
        [Description("Organization legal name (required for Organization type)")] string? legalName = null,
        [Description("Organization trading/brand name")] string? tradingName = null,
        [Description("Legal form (e.g., 'SAS', 'SARL')")] string? legalForm = null,
        [Description("Company registration number")] string? registrationNumber = null,
        [Description("Email address — creates an Email contact channel")] string? email = null,
        [Description("Phone number — creates a Phone contact channel")] string? phone = null,
        [Description("VAT number — creates a VAT identifier")] string? vatNumber = null,
        CancellationToken cancellationToken = default)
    {
        McpTenantAccessContext access = await McpTenantAuthorization
            .RequireAccessAsync(services, TenantAccessRequirement.Write, cancellationToken)
            .ConfigureAwait(false);
        string tenant = access.TenantId;

        PartyType partyType = ParsePartyType(type);

        // Generate party ID
        string partyId = Guid.NewGuid().ToString();

        // Build person or organization details
        PersonDetails? personDetails = null;
        OrganizationDetails? orgDetails = null;

        if (partyType == PartyType.Person)
        {
            if (string.IsNullOrWhiteSpace(lastName))
            {
                throw new InvalidOperationException("Last name is required for Person party type.");
            }

            DateTimeOffset? dob = ParseDateOfBirth(dateOfBirth);

            personDetails = new PersonDetails
            {
                FirstName = firstName ?? string.Empty,
                LastName = lastName,
                DateOfBirth = dob,
                Prefix = prefix,
                Suffix = suffix,
            };
        }
        else if (partyType == PartyType.Organization)
        {
            if (string.IsNullOrWhiteSpace(legalName))
            {
                throw new InvalidOperationException("Legal name is required for Organization party type.");
            }

            orgDetails = new OrganizationDetails
            {
                LegalName = legalName,
                TradingName = tradingName,
                LegalForm = legalForm,
                RegistrationNumber = registrationNumber,
            };
        }

        // Build contact channels
        List<AddContactChannel> contactChannels = [];

        if (!string.IsNullOrWhiteSpace(email))
        {
            contactChannels.Add(new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = Guid.NewGuid().ToString(),
                Type = ContactChannelType.Email,
                Value = email,
                IsPreferred = true,
            });
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            contactChannels.Add(new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = Guid.NewGuid().ToString(),
                Type = ContactChannelType.Phone,
                Value = phone,
                IsPreferred = false,
            });
        }

        // Build identifiers
        List<AddIdentifier> identifiers = [];

        if (!string.IsNullOrWhiteSpace(vatNumber))
        {
            identifiers.Add(new AddIdentifier
            {
                PartyId = partyId,
                IdentifierId = Guid.NewGuid().ToString(),
                Type = IdentifierType.VAT,
                Value = vatNumber,
            });
        }

        // Construct the composite command
        var command = new CreatePartyComposite
        {
            PartyId = partyId,
            Type = partyType,
            PersonDetails = personDetails,
            OrganizationDetails = orgDetails,
            ContactChannels = contactChannels,
            Identifiers = identifiers,
        };

        // Validate using FluentValidation
        IValidator<CreatePartyComposite> validator = services.GetRequiredService<IValidator<CreatePartyComposite>>();
        ValidationResult validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            string errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        // Dispatch command via ICommandRouter
        ICommandRouter commandRouter = services.GetRequiredService<ICommandRouter>();

        var submitCommand = new SubmitCommand(
            MessageId: Guid.NewGuid().ToString(),
            Tenant: tenant,
            Domain: "party",
            AggregateId: partyId,
            CommandType: nameof(CreatePartyComposite),
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: Guid.NewGuid().ToString(),
            UserId: access.UserId);

        CommandProcessingResult result = await commandRouter
            .RouteCommandAsync(submitCommand, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted)
        {
            throw new InvalidOperationException($"Party creation failed: {result.ErrorMessage}");
        }

        // Query the projection actor for the complete PartyDetail
        IActorProxyFactory actorProxyFactory = services.GetRequiredService<IActorProxyFactory>();
        var actorId = new ActorId($"{tenant}:party-detail:{partyId}");
        IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            actorId, nameof(PartyDetailProjectionActor));

        PartyDetail? detail = await proxy.GetDetailAsync().ConfigureAwait(false);

        // Fallback if projection hasn't caught up yet (eventual consistency)
        if (detail is null)
        {
            (string displayName, string sortName) = DeriveDisplayName(partyType, personDetails, orgDetails);

            detail = new PartyDetail
            {
                Id = partyId,
                Type = partyType,
                IsActive = true,
                DisplayName = displayName,
                SortName = sortName,
                PersonDetails = personDetails,
                OrganizationDetails = orgDetails,
                ContactChannels = contactChannels.Select(c => new ContactChannel
                {
                    Id = c.ContactChannelId,
                    Type = c.Type,
                    Value = c.Value,
                    IsPreferred = c.IsPreferred,
                }).ToList(),
                Identifiers = identifiers.Select(i => new PartyIdentifier
                {
                    Id = i.IdentifierId,
                    Type = i.Type,
                    Value = i.Value,
                }).ToList(),
                CreatedAt = DateTimeOffset.UtcNow,
                LastModifiedAt = DateTimeOffset.UtcNow,
            };
        }

        return JsonSerializer.Serialize(detail, McpSessionContext.JsonOptions);
    }

    private static PartyType ParsePartyType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new InvalidOperationException("Party type is required. Must be 'Person' or 'Organization'.");
        }

        if (!Enum.TryParse(type, ignoreCase: true, out PartyType partyType) || partyType == PartyType.Unknown)
        {
            throw new InvalidOperationException($"Invalid party type '{type}'. Must be 'Person' or 'Organization'.");
        }

        return partyType;
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

    private static (string DisplayName, string SortName) DeriveDisplayName(
        PartyType type,
        PersonDetails? person,
        OrganizationDetails? organization)
    {
        return type switch
        {
            PartyType.Person when person is not null =>
                ($"{person.FirstName} {person.LastName}", $"{person.LastName}, {person.FirstName}"),
            PartyType.Organization when organization is not null =>
                (organization.LegalName, organization.LegalName),
            _ => throw new InvalidOperationException($"Unsupported party type: {type}"),
        };
    }
}
