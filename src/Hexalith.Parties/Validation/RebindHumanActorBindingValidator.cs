using FluentValidation;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.Validation;

/// <summary>Validates safe attribution intent before protected state is accessed.</summary>
public sealed class RebindHumanActorBindingValidator : AbstractValidator<RebindHumanActorBinding>
{
    public RebindHumanActorBindingValidator()
    {
        RuleFor(command => command.LogicalId).NotEmpty();
        RuleFor(command => command.PolicyId).NotEmpty();
        RuleFor(command => command.ActorRevision).GreaterThan(0);
        RuleFor(command => command.ExpectedPartyRevision).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ExpectedBindingVersion).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ActorId).Must(value => value is { Length: 26 } && value[0] <= '7'
            && value.All(character => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(character, StringComparison.Ordinal)));
        RuleFor(command => command).Must(command =>
        {
            try
            {
                return new AggregateIdentity(command.TenantId, "party", command.PartyId).TenantId == command.TenantId;
            }
            catch (ArgumentException)
            {
                return false;
            }
        });
    }
}
