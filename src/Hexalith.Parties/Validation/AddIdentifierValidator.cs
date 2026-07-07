using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class AddIdentifierValidator : AbstractValidator<AddIdentifier>
{
    public AddIdentifierValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

        RuleFor(x => x.IdentifierId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("IdentifierId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("IdentifierId must be a support-safe identifier.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Type must be a valid IdentifierType.");

        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Value is required.");
    }
}
