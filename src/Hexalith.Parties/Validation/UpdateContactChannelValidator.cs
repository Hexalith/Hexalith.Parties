using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class UpdateContactChannelValidator : AbstractValidator<UpdateContactChannel>
{
    public UpdateContactChannelValidator()
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
    }
}
