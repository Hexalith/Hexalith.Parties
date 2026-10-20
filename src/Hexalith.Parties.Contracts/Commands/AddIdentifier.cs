using System.ComponentModel;
using System.Text.Json.Serialization;

using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Supplied add identifier for a typed party command.</summary>
public sealed record AddIdentifier
{
    /// <summary>The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.</summary>
    [Description("The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.")]
    [HexalithIdentifier]
    public required string PartyId { get; init; }

    /// <summary>The supplied IdentifierId, at most 128 characters; distinct from the business identifier Value, which has a 512 UTF-16-unit limit. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.</summary>
    [Description("The supplied IdentifierId, at most 128 characters; distinct from the business identifier Value, which has a 512 UTF-16-unit limit. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.")]
    [HexalithIdentifier]
    public required string IdentifierId { get; init; }

    /// <summary>The business identifier kind: VAT, SIRET, NationalId, CompanyRegistration, TaxId or Other.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<IdentifierType>))]
    [Description("The business identifier kind: VAT, SIRET, NationalId, CompanyRegistration, TaxId or Other.")]
    public required IdentifierType Type { get; init; }

    /// <summary>The business identifier Value, with at least one non-whitespace character and at most 512 UTF-16 units, stored without trimming; the 128-character IdentifierId limit does not apply to this value.</summary>
    [PersonalData]
    [Description("The business identifier Value, with at least one non-whitespace character and at most 512 UTF-16 units, stored without trimming; the 128-character IdentifierId limit does not apply to this value.")]
    public required string Value { get; init; }
}
