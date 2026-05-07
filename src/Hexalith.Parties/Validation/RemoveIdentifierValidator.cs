using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.Validation;

public sealed class RemoveIdentifierValidator : AbstractValidator<RemoveIdentifier>
{
    public RemoveIdentifierValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.IdentifierId)
            .NotEmpty()
            .WithMessage("IdentifierId is required.");
    }
}
