using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class AddContactChannelValidator : AbstractValidator<AddContactChannel>
{
    public AddContactChannelValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

        RuleFor(x => x.ContactChannelId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("ContactChannelId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("ContactChannelId must be a support-safe identifier.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Type must be a valid ContactChannelType.");

        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Value is required.");
    }
}
