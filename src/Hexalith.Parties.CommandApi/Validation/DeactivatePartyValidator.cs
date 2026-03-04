using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.CommandApi.Validation;

public sealed class DeactivatePartyValidator : AbstractValidator<DeactivateParty>
{
    public DeactivatePartyValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");
    }
}
