using System.ComponentModel;
using System.Globalization;

using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using ModelContextProtocol.Server;

namespace Hexalith.Parties.Mcp.Tools;

[McpServerToolType]
internal sealed class PartiesMcpTools(
    IPartiesCommandClient commandClient,
    IPartiesQueryClient queryClient,
    IPartiesMcpRequestContextAccessor contextAccessor)
{
    [McpServerTool(Name = PartiesMcpToolNames.GetParty, Title = "Get Party", ReadOnly = true)]
    [Description("Gets a party by identifier through the Parties EventStore client boundary.")]
    public async Task<PartiesMcpToolResult> GetParty(
        [Description("The party identifier to retrieve.")] string partyId,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            PartiesMcpToolNames.GetParty,
            async () =>
            {
                PartiesMcpToolResult? validation = ValidateContextAndPartyId(PartiesMcpToolNames.GetParty, partyId);
                if (validation is not null)
                {
                    return validation;
                }

                PartyDetail party = await queryClient.GetPartyAsync(partyId, cancellationToken).ConfigureAwait(false);
                return PartiesMcpToolResult.Succeeded(PartiesMcpToolNames.GetParty, party);
            }).ConfigureAwait(false);

    [McpServerTool(Name = PartiesMcpToolNames.FindParties, Title = "Find Parties", ReadOnly = true)]
    [Description("Finds parties using forgiving search, paging, type, and active filters through the Parties EventStore client boundary.")]
    public async Task<PartiesMcpToolResult> FindParties(
        [Description("Search text. Empty or omitted text lists parties.")] string? query = null,
        [Description("One-based page number.")] int page = 1,
        [Description("Requested page size.")] int pageSize = 20,
        [Description("Optional party type filter, such as Person or Organization.")] string? type = null,
        [Description("Optional active-state filter.")] bool? active = null,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            PartiesMcpToolNames.FindParties,
            async () =>
            {
                PartiesMcpToolResult? validation = ValidateContext(PartiesMcpToolNames.FindParties);
                if (validation is not null)
                {
                    return validation;
                }

                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                if (!string.IsNullOrWhiteSpace(query))
                {
                    PagedResult<PartySearchResult> searchResult = await queryClient
                        .SearchPartiesAsync(query.Trim(), page, pageSize, cancellationToken)
                        .ConfigureAwait(false);

                    if (!TryParseEnum(type, out PartyType? searchPartyType))
                    {
                        return ValidationFailed(PartiesMcpToolNames.FindParties, "party type");
                    }

                    return PartiesMcpToolResult.Succeeded(
                        PartiesMcpToolNames.FindParties,
                        FilterSearchResult(searchResult, searchPartyType, active));
                }

                if (!TryParseEnum(type, out PartyType? partyType))
                {
                    return ValidationFailed(PartiesMcpToolNames.FindParties, "party type");
                }

                PagedResult<PartyIndexEntry> listResult = await queryClient
                    .ListPartiesAsync(page, pageSize, partyType, active, null, null, null, null, cancellationToken)
                    .ConfigureAwait(false);
                return PartiesMcpToolResult.Succeeded(PartiesMcpToolNames.FindParties, listResult);
            }).ConfigureAwait(false);

    [McpServerTool(Name = PartiesMcpToolNames.CreateParty, Title = "Create Party", Destructive = true)]
    [Description("Creates a person or organization party from forgiving AI-friendly input through the Parties EventStore client boundary.")]
    public async Task<PartiesMcpToolResult> CreateParty(
        [Description("Optional caller-supplied party identifier.")] string? partyId = null,
        [Description("Party kind, such as person or organization.")] string? partyType = null,
        [Description("Person given name when creating a person.")] string? givenName = null,
        [Description("Person family name when creating a person.")] string? familyName = null,
        [Description("Alias for givenName, preserved for pre-pivot MCP callers.")] string? firstName = null,
        [Description("Alias for familyName, preserved for pre-pivot MCP callers.")] string? lastName = null,
        [Description("Person date of birth as an ISO 8601 date or date-time.")] string? dateOfBirth = null,
        [Description("Optional person name prefix.")] string? prefix = null,
        [Description("Optional person name suffix.")] string? suffix = null,
        [Description("Organization legal name when creating an organization.")] string? legalName = null,
        [Description("Optional organization trading or brand name.")] string? tradingName = null,
        [Description("Optional organization legal form.")] string? legalForm = null,
        [Description("Optional organization registration number.")] string? registrationNumber = null,
        [Description("Optional email contact channel value.")] string? email = null,
        [Description("Optional phone contact channel value.")] string? phone = null,
        [Description("Optional VAT number alias for identifierValue.")] string? vatNumber = null,
        [Description("Optional identifier type, such as TaxId, VAT, SIRET, NationalId, CompanyRegistration, or Other.")] string? identifierType = null,
        [Description("Optional identifier value.")] string? identifierValue = null,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            PartiesMcpToolNames.CreateParty,
            async () =>
            {
                PartiesMcpToolResult? validation = ValidateContext(PartiesMcpToolNames.CreateParty);
                if (validation is not null)
                {
                    return validation;
                }

                string? effectiveGivenName = FirstNonEmpty(givenName, firstName);
                string? effectiveFamilyName = FirstNonEmpty(familyName, lastName);
                string? effectiveIdentifierType = FirstNonEmpty(identifierType, !string.IsNullOrWhiteSpace(vatNumber) ? "VAT" : null);
                string? effectiveIdentifierValue = FirstNonEmpty(identifierValue, vatNumber);

                PartyType type = ResolveCreateType(partyType, effectiveGivenName, effectiveFamilyName, legalName);
                if (type == PartyType.Unknown)
                {
                    return ValidationFailed(PartiesMcpToolNames.CreateParty, "party type");
                }

                string effectivePartyId = string.IsNullOrWhiteSpace(partyId) ? NewId() : partyId.Trim();
                CreatePartyComposite? command = BuildCreateCommand(
                    effectivePartyId,
                    type,
                    effectiveGivenName,
                    effectiveFamilyName,
                    dateOfBirth,
                    prefix,
                    suffix,
                    legalName,
                    tradingName,
                    legalForm,
                    registrationNumber,
                    email,
                    phone,
                    effectiveIdentifierType,
                    effectiveIdentifierValue);
                if (command is null)
                {
                    return ValidationFailed(PartiesMcpToolNames.CreateParty, "party details");
                }

                string correlationId = await commandClient
                    .CreatePartyCompositeAsync(command, cancellationToken)
                    .ConfigureAwait(false);
                return PartiesMcpToolResult.Accepted(PartiesMcpToolNames.CreateParty, correlationId);
            }).ConfigureAwait(false);

    [McpServerTool(Name = PartiesMcpToolNames.UpdateParty, Title = "Update Party", Destructive = true)]
    [Description("Updates a Parties record using patch semantics where the route partyId is authoritative.")]
    public async Task<PartiesMcpToolResult> UpdateParty(
        [Description("The authoritative party identifier to update.")] string partyId,
        [Description("Optional person given name patch.")] string? givenName = null,
        [Description("Optional person family name patch.")] string? familyName = null,
        [Description("Alias for givenName, preserved for pre-pivot MCP callers.")] string? firstName = null,
        [Description("Alias for familyName, preserved for pre-pivot MCP callers.")] string? lastName = null,
        [Description("Optional person date-of-birth patch as an ISO 8601 date or date-time.")] string? dateOfBirth = null,
        [Description("Optional person name prefix patch.")] string? prefix = null,
        [Description("Optional person name suffix patch.")] string? suffix = null,
        [Description("Optional organization legal name patch.")] string? legalName = null,
        [Description("Optional organization trading or brand name patch.")] string? tradingName = null,
        [Description("Optional organization legal form patch.")] string? legalForm = null,
        [Description("Optional organization registration number patch.")] string? registrationNumber = null,
        [Description("Optional active-state patch.")] bool? active = null,
        [Description("Optional email contact channel to add.")] string? addEmail = null,
        [Description("Optional phone contact channel to add.")] string? addPhone = null,
        [Description("Optional contact channel id to update.")] string? updateContactChannelId = null,
        [Description("Optional replacement contact channel type for the updated contact.")] string? updateContactChannelType = null,
        [Description("Optional replacement contact channel value for the updated contact.")] string? updateContactChannelValue = null,
        [Description("Optional preferred flag for the updated contact channel.")] bool? updateContactChannelPreferred = null,
        [Description("Optional contact channel id to remove.")] string? removeContactChannelId = null,
        [Description("Comma-separated contact channel ids to remove, preserved for pre-pivot MCP callers.")] string? removeContactChannelIds = null,
        [Description("Optional VAT number alias for addIdentifierValue.")] string? addVatNumber = null,
        [Description("Optional identifier type to add.")] string? addIdentifierType = null,
        [Description("Optional identifier value to add.")] string? addIdentifierValue = null,
        [Description("Optional identifier id to remove.")] string? removeIdentifierId = null,
        [Description("Comma-separated identifier ids to remove, preserved for pre-pivot MCP callers.")] string? removeIdentifierIds = null,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            PartiesMcpToolNames.UpdateParty,
            async () =>
            {
                PartiesMcpToolResult? validation = ValidateContextAndPartyId(PartiesMcpToolNames.UpdateParty, partyId);
                if (validation is not null)
                {
                    return validation;
                }

                string? effectiveGivenName = FirstNonEmpty(givenName, firstName);
                string? effectiveFamilyName = FirstNonEmpty(familyName, lastName);
                string? effectiveAddIdentifierType = FirstNonEmpty(addIdentifierType, !string.IsNullOrWhiteSpace(addVatNumber) ? "VAT" : null);
                string? effectiveAddIdentifierValue = FirstNonEmpty(addIdentifierValue, addVatNumber);
                string? effectiveRemoveContactIds = CombineCsv(removeContactChannelId, removeContactChannelIds);
                string? effectiveRemoveIdentifierIds = CombineCsv(removeIdentifierId, removeIdentifierIds);
                bool needsCurrentParty = HasAny(effectiveGivenName, effectiveFamilyName, dateOfBirth, prefix, suffix, legalName, tradingName, legalForm, registrationNumber)
                    || HasAny(updateContactChannelId, updateContactChannelType, updateContactChannelValue)
                    || updateContactChannelPreferred.HasValue;
                PartyDetail? currentParty = needsCurrentParty
                    ? await queryClient.GetPartyAsync(partyId, cancellationToken).ConfigureAwait(false)
                    : null;

                UpdatePartyComposite? command = BuildUpdateCommand(
                    partyId,
                    currentParty,
                    effectiveGivenName,
                    effectiveFamilyName,
                    dateOfBirth,
                    prefix,
                    suffix,
                    legalName,
                    tradingName,
                    legalForm,
                    registrationNumber,
                    addEmail,
                    addPhone,
                    updateContactChannelId,
                    updateContactChannelType,
                    updateContactChannelValue,
                    updateContactChannelPreferred,
                    effectiveRemoveContactIds,
                    effectiveAddIdentifierType,
                    effectiveAddIdentifierValue,
                    effectiveRemoveIdentifierIds);
                if (command is null && !active.HasValue)
                {
                    return PartiesMcpToolResult.Failed(
                        PartiesMcpToolNames.UpdateParty,
                        "validation_failed",
                        "parties-mcp-no-change",
                        "No supported update fields were provided.");
                }

                List<string> correlationIds = [];
                if (command is not null)
                {
                    correlationIds.Add(await commandClient
                        .UpdatePartyCompositeAsync(partyId, command, cancellationToken)
                        .ConfigureAwait(false));
                }

                if (active.HasValue)
                {
                    correlationIds.Add(active.Value
                        ? await commandClient.ReactivatePartyAsync(partyId, cancellationToken).ConfigureAwait(false)
                        : await commandClient.DeactivatePartyAsync(partyId, cancellationToken).ConfigureAwait(false));
                }

                return correlationIds.Count == 1
                    ? PartiesMcpToolResult.Accepted(PartiesMcpToolNames.UpdateParty, correlationIds[0])
                    : PartiesMcpToolResult.Accepted(PartiesMcpToolNames.UpdateParty, correlationIds);
            }).ConfigureAwait(false);

    [McpServerTool(Name = PartiesMcpToolNames.DeleteParty, Title = "Delete Party", Destructive = true, Idempotent = true)]
    [Description("Soft deactivates a Parties record while preserving idempotent already-inactive behavior when the client contract can observe it.")]
    public async Task<PartiesMcpToolResult> DeleteParty(
        [Description("The party identifier to deactivate.")] string partyId,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            PartiesMcpToolNames.DeleteParty,
            async () =>
            {
                PartiesMcpToolResult? validation = ValidateContextAndPartyId(PartiesMcpToolNames.DeleteParty, partyId);
                if (validation is not null)
                {
                    return validation;
                }

                PartyDetail party = await queryClient.GetPartyAsync(partyId, cancellationToken).ConfigureAwait(false);
                if (!party.IsActive)
                {
                    return PartiesMcpToolResult.Succeeded(
                        PartiesMcpToolNames.DeleteParty,
                        new { partyId, idempotent = true },
                        "parties-mcp-delete-idempotent");
                }

                string correlationId = await commandClient.DeactivatePartyAsync(partyId, cancellationToken).ConfigureAwait(false);
                return PartiesMcpToolResult.Accepted(PartiesMcpToolNames.DeleteParty, correlationId);
            }).ConfigureAwait(false);

    [McpServerTool(Name = PartiesMcpToolNames.GetPartyNameAt, Title = "Get Party Name At", ReadOnly = true)]
    [Description("Compatibility tool for Parties temporal name lookup through the Parties EventStore query client path.")]
    public async Task<PartiesMcpToolResult> GetPartyNameAt(
        [Description("The party identifier to inspect.")] string partyId,
        [Description("The instant for the temporal lookup, as an ISO 8601 value.")] string asOf,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            PartiesMcpToolNames.GetPartyNameAt,
            async () =>
            {
                PartiesMcpToolResult? validation = ValidateContextAndPartyId(PartiesMcpToolNames.GetPartyNameAt, partyId);
                if (validation is not null)
                {
                    return validation;
                }

                if (!DateTimeOffset.TryParse(asOf, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset instant))
                {
                    return ValidationFailed(PartiesMcpToolNames.GetPartyNameAt, "asOf");
                }

                PartyDetail party = await queryClient.GetPartyAsync(partyId, cancellationToken).ConfigureAwait(false);
                if (party.IsErased)
                {
                    return PartiesMcpToolResult.Failed(
                        PartiesMcpToolNames.GetPartyNameAt,
                        "not_found",
                        "parties-mcp-party-erased",
                        "The requested Parties resource was not found.");
                }

                if (party.NameHistory.Count == 0)
                {
                    return PartiesMcpToolResult.Failed(
                        PartiesMcpToolNames.GetPartyNameAt,
                        "not_found",
                        "parties-mcp-name-history-unavailable",
                        "Name history is not available for the requested Parties resource.");
                }

                NameHistoryEntry? history = party.NameHistory
                    .Where(entry => entry.ChangedAt <= instant)
                    .OrderByDescending(entry => entry.ChangedAt)
                    .FirstOrDefault();
                if (history is null)
                {
                    return PartiesMcpToolResult.Failed(
                        PartiesMcpToolNames.GetPartyNameAt,
                        "not_found",
                        "parties-mcp-name-not-effective",
                        "The Parties resource did not have an effective name at the requested instant.");
                }

                return PartiesMcpToolResult.Succeeded(
                    PartiesMcpToolNames.GetPartyNameAt,
                    new TemporalNameResult
                    {
                        PartyId = party.Id,
                        AsOf = instant,
                        DisplayName = history.DisplayName,
                        SortName = history.SortName,
                    });
            }).ConfigureAwait(false);

    private static async Task<PartiesMcpToolResult> ExecuteAsync(
        string toolName,
        Func<Task<PartiesMcpToolResult>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (PartiesClientException ex)
        {
            return MapClientException(toolName, ex);
        }
        catch (FormatException)
        {
            return PartiesMcpToolResult.Failed(
                toolName,
                "validation_failed",
                "parties-mcp-validation-failed",
                "One or more Parties MCP arguments were invalid.");
        }
        catch (OperationCanceledException)
        {
            return PartiesMcpToolResult.Failed(
                toolName,
                "canceled",
                "parties-mcp-canceled",
                "The Parties operation was canceled.");
        }
        catch (TimeoutException)
        {
            return PartiesMcpToolResult.Failed(
                toolName,
                "timeout",
                "parties-mcp-timeout",
                "The downstream Parties/EventStore operation timed out.");
        }
        catch (HttpRequestException)
        {
            return PartiesMcpToolResult.Failed(
                toolName,
                "downstream_failed",
                "parties-mcp-downstream-failed",
                "The downstream Parties/EventStore gateway could not be reached.");
        }
    }

    private PartiesMcpToolResult? ValidateContextAndPartyId(string toolName, string partyId)
        => ValidateContext(toolName) ?? (string.IsNullOrWhiteSpace(partyId) ? ValidationFailed(toolName, "partyId") : null);

    private PartiesMcpToolResult? ValidateContext(string toolName)
        => contextAccessor.Current is null
            ? PartiesMcpToolResult.Failed(
                toolName,
                "missing_context",
                "parties-mcp-missing-context",
                "Tenant and user context are required before Parties MCP tools can call the EventStore gateway.")
            : null;

    private static PartiesMcpToolResult ValidationFailed(string toolName, string field)
        => PartiesMcpToolResult.Failed(
            toolName,
            "validation_failed",
            "parties-mcp-validation-failed",
            $"The {field} argument is missing or invalid.");

    private static PartiesMcpToolResult MapClientException(string toolName, PartiesClientException ex)
    {
        string category = ex.Status switch
        {
            400 or 422 => "validation_failed",
            401 => "unauthorized",
            403 => "forbidden",
            404 or 410 => "not_found",
            409 => "conflict",
            408 => "timeout",
            >= 500 => "downstream_failed",
            _ => "rejected",
        };

        return PartiesMcpToolResult.Failed(
            toolName,
            category,
            $"parties-mcp-{category.Replace('_', '-')}",
            SafeMessage(category),
            ex.CorrelationId);
    }

    private static string SafeMessage(string category)
        => category switch
        {
            "validation_failed" => "The Parties gateway rejected the request as invalid.",
            "unauthorized" => "Authentication is required by the Parties gateway.",
            "forbidden" => "The Parties gateway denied access for the current context.",
            "not_found" => "The requested Parties resource was not found.",
            "conflict" => "The Parties gateway reported a conflict for the requested operation.",
            "timeout" => "The Parties gateway timed out while processing the request.",
            "downstream_failed" => "The downstream Parties/EventStore gateway failed.",
            _ => "The Parties gateway rejected the requested operation.",
        };

    private static PagedResult<PartySearchResult> FilterSearchResult(
        PagedResult<PartySearchResult> result,
        PartyType? type,
        bool? active)
    {
        if (type is null && active is null)
        {
            return result;
        }

        PartySearchResult[] filtered =
        [
            .. result.Items.Where(item =>
                (type is null || item.Party.Type == type)
                && (active is null || item.Party.IsActive == active)),
        ];

        return result with
        {
            Items = filtered,
            TotalCount = filtered.Length,
            TotalPages = filtered.Length == 0 ? 0 : (int)Math.Ceiling(filtered.Length / (double)Math.Max(1, result.PageSize)),
        };
    }

    private static CreatePartyComposite? BuildCreateCommand(
        string partyId,
        PartyType type,
        string? givenName,
        string? familyName,
        string? dateOfBirth,
        string? prefix,
        string? suffix,
        string? legalName,
        string? tradingName,
        string? legalForm,
        string? registrationNumber,
        string? email,
        string? phone,
        string? identifierType,
        string? identifierValue)
    {
        PersonDetails? person = type == PartyType.Person
            ? BuildPersonDetails(givenName, familyName, dateOfBirth, prefix, suffix)
            : null;
        OrganizationDetails? organization = type == PartyType.Organization
            ? BuildOrganizationDetails(legalName, tradingName, legalForm, registrationNumber)
            : null;

        if ((type == PartyType.Person && person is null) || (type == PartyType.Organization && organization is null))
        {
            return null;
        }

        List<AddContactChannel> contacts = [];
        if (!string.IsNullOrWhiteSpace(email))
        {
            contacts.Add(new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = NewId(),
                Type = ContactChannelType.Email,
                Value = email.Trim(),
                IsPreferred = contacts.Count == 0,
            });
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            contacts.Add(new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = NewId(),
                Type = ContactChannelType.Phone,
                Value = phone.Trim(),
                IsPreferred = contacts.Count == 0,
            });
        }

        List<AddIdentifier> identifiers = [];
        if (!string.IsNullOrWhiteSpace(identifierValue))
        {
            if (!TryParseEnum(identifierType, out IdentifierType? parsedIdentifierType))
            {
                return null;
            }

            identifiers.Add(new AddIdentifier
            {
                PartyId = partyId,
                IdentifierId = NewId(),
                Type = parsedIdentifierType ?? IdentifierType.Other,
                Value = identifierValue.Trim(),
            });
        }

        return new CreatePartyComposite
        {
            PartyId = partyId,
            Type = type,
            PersonDetails = person,
            OrganizationDetails = organization,
            ContactChannels = contacts,
            Identifiers = identifiers,
        };
    }

    private static UpdatePartyComposite? BuildUpdateCommand(
        string partyId,
        PartyDetail? currentParty,
        string? givenName,
        string? familyName,
        string? dateOfBirth,
        string? prefix,
        string? suffix,
        string? legalName,
        string? tradingName,
        string? legalForm,
        string? registrationNumber,
        string? addEmail,
        string? addPhone,
        string? updateContactChannelId,
        string? updateContactChannelType,
        string? updateContactChannelValue,
        bool? updateContactChannelPreferred,
        string? removeContactChannelId,
        string? addIdentifierType,
        string? addIdentifierValue,
        string? removeIdentifierId)
    {
        PersonDetails? person = HasAny(givenName, familyName, dateOfBirth, prefix, suffix)
            ? BuildPersonDetails(givenName, familyName, dateOfBirth, prefix, suffix, currentParty?.PersonDetails)
            : null;
        OrganizationDetails? organization = HasAny(legalName, tradingName, legalForm, registrationNumber)
            ? BuildOrganizationDetails(legalName, tradingName, legalForm, registrationNumber, currentParty?.OrganizationDetails)
            : null;

        List<AddContactChannel> addContacts = [];
        if (!string.IsNullOrWhiteSpace(addEmail))
        {
            addContacts.Add(new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = NewId(),
                Type = ContactChannelType.Email,
                Value = addEmail.Trim(),
                IsPreferred = addContacts.Count == 0,
            });
        }

        if (!string.IsNullOrWhiteSpace(addPhone))
        {
            addContacts.Add(new AddContactChannel
            {
                PartyId = partyId,
                ContactChannelId = NewId(),
                Type = ContactChannelType.Phone,
                Value = addPhone.Trim(),
                IsPreferred = addContacts.Count == 0,
            });
        }

        List<UpdateContactChannel> updateContacts = [];
        if (HasAny(updateContactChannelId, updateContactChannelType, updateContactChannelValue) || updateContactChannelPreferred.HasValue)
        {
            if (string.IsNullOrWhiteSpace(updateContactChannelId))
            {
                return null;
            }

            ContactChannel? currentContact = currentParty?.ContactChannels
                .FirstOrDefault(channel => string.Equals(channel.Id, updateContactChannelId.Trim(), StringComparison.Ordinal));
            if (currentParty is not null && currentContact is null)
            {
                return null;
            }

            if (!TryParseEnum(updateContactChannelType, out ContactChannelType? parsedContactType))
            {
                return null;
            }

            updateContacts.Add(new UpdateContactChannel
            {
                PartyId = partyId,
                ContactChannelId = updateContactChannelId.Trim(),
                Type = parsedContactType ?? currentContact?.Type,
                Value = string.IsNullOrWhiteSpace(updateContactChannelValue) ? currentContact?.Value : updateContactChannelValue.Trim(),
                IsPreferred = updateContactChannelPreferred ?? currentContact?.IsPreferred,
            });
        }

        List<AddIdentifier> addIdentifiers = [];
        if (!string.IsNullOrWhiteSpace(addIdentifierValue))
        {
            if (!TryParseEnum(addIdentifierType, out IdentifierType? parsedIdentifierType))
            {
                return null;
            }

            addIdentifiers.Add(new AddIdentifier
            {
                PartyId = partyId,
                IdentifierId = NewId(),
                Type = parsedIdentifierType ?? IdentifierType.Other,
                Value = addIdentifierValue.Trim(),
            });
        }

        string[] removeContacts = SplitCsv(removeContactChannelId);
        string[] removeIdentifiers = SplitCsv(removeIdentifierId);

        return person is null
            && organization is null
            && addContacts.Count == 0
            && updateContacts.Count == 0
            && removeContacts.Length == 0
            && addIdentifiers.Count == 0
            && removeIdentifiers.Length == 0
            ? null
            : new UpdatePartyComposite
            {
                PartyId = partyId,
                PersonDetails = person,
                OrganizationDetails = organization,
                AddContactChannels = addContacts,
                UpdateContactChannels = updateContacts,
                RemoveContactChannelIds = removeContacts,
                AddIdentifiers = addIdentifiers,
                RemoveIdentifierIds = removeIdentifiers,
            };
    }

    private static PersonDetails? BuildPersonDetails(
        string? givenName,
        string? familyName,
        string? dateOfBirth = null,
        string? prefix = null,
        string? suffix = null,
        PersonDetails? current = null)
    {
        if (string.IsNullOrWhiteSpace(familyName) && string.IsNullOrWhiteSpace(current?.LastName))
        {
            return null;
        }

        return new PersonDetails
        {
            FirstName = string.IsNullOrWhiteSpace(givenName) ? current?.FirstName ?? string.Empty : givenName.Trim(),
            LastName = string.IsNullOrWhiteSpace(familyName) ? current?.LastName ?? string.Empty : familyName.Trim(),
            DateOfBirth = dateOfBirth is null ? current?.DateOfBirth : ParseDateOfBirth(dateOfBirth),
            Prefix = prefix ?? current?.Prefix,
            Suffix = suffix ?? current?.Suffix,
        };
    }

    private static OrganizationDetails? BuildOrganizationDetails(
        string? legalName,
        string? tradingName = null,
        string? legalForm = null,
        string? registrationNumber = null,
        OrganizationDetails? current = null)
    {
        if (string.IsNullOrWhiteSpace(legalName) && string.IsNullOrWhiteSpace(current?.LegalName))
        {
            return null;
        }

        return new OrganizationDetails
        {
            LegalName = string.IsNullOrWhiteSpace(legalName) ? current?.LegalName ?? string.Empty : legalName.Trim(),
            TradingName = tradingName ?? current?.TradingName,
            LegalForm = legalForm ?? current?.LegalForm,
            RegistrationNumber = registrationNumber ?? current?.RegistrationNumber,
            IsNaturalPerson = current?.IsNaturalPerson ?? false,
        };
    }

    private static PartyType ResolveCreateType(string? partyType, string? givenName, string? familyName, string? legalName)
    {
        if (TryParseEnum(partyType, out PartyType? parsed) && parsed is not null and not PartyType.Unknown)
        {
            return parsed.Value;
        }

        if (!string.IsNullOrWhiteSpace(legalName))
        {
            return PartyType.Organization;
        }

        return HasAny(givenName, familyName) ? PartyType.Person : PartyType.Unknown;
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum? result)
        where TEnum : struct, Enum
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse(value, ignoreCase: true, out TEnum parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private static bool HasAny(params string?[] values)
        => values.Any(value => !string.IsNullOrWhiteSpace(value));

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? CombineCsv(params string?[] values)
    {
        string[] parts = [.. values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))];

        return parts.Length == 0 ? null : string.Join(",", parts);
    }

    private static string[] SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static DateTimeOffset? ParseDateOfBirth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
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

        return DateTimeOffset.TryParseExact(
            value,
            supportedFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out DateTimeOffset parsed)
            ? parsed
            : throw new FormatException("Date of birth must be a valid ISO 8601 date or date-time.");
    }

    private static string NewId()
        => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
}
