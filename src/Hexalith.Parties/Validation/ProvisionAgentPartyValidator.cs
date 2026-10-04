using FluentValidation;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Validation;

/// <summary>Validates versioned deterministic Agent provisioning intent before protected state is accessed.</summary>
public sealed class ProvisionAgentPartyValidator : AbstractValidator<ProvisionAgentParty>
{
    public ProvisionAgentPartyValidator()
    {
        RuleFor(command => command.Identity).NotNull();
        When(command => command.Identity is not null, () =>
        {
            RuleFor(command => command.Identity.ContractVersion).Equal(1);
            RuleFor(command => command.Identity.LogicalId).NotEmpty();
            RuleFor(command => command.Identity.CreationFingerprint).Must(value => value is { Length: 64 } && value.All(Uri.IsHexDigit));
            RuleFor(command => command.Identity).Must(identity =>
            {
                try
                {
                    return AgentPartyIdMapping.Create(identity.TenantId, identity.AgentId) == identity.PartyId;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            });
        });
    }
}
