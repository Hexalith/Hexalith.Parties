using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class UpdatePersonDetailsValidator : AbstractValidator<UpdatePersonDetails>
{
    public UpdatePersonDetailsValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

        RuleFor(x => x.PersonDetails)
            .NotNull()
            .WithMessage("PersonDetails is required.");
    }
}
