using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class ReactivatePartyValidator : AbstractValidator<ReactivateParty>
{
    public ReactivatePartyValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");
    }
}
