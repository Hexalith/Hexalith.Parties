using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.Validation;

public sealed class AddContactChannelValidator : AbstractValidator<AddContactChannel>
{
    public AddContactChannelValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.ContactChannelId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("ContactChannelId is required.")
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("ContactChannelId must be a valid GUID.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Type must be a valid ContactChannelType.");

        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Value is required.");
    }
}
