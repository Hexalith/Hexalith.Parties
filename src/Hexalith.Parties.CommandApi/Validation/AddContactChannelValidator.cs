using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.CommandApi.Validation;

public sealed class AddContactChannelValidator : AbstractValidator<AddContactChannel>
{
    public AddContactChannelValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.ContactChannelId)
            .NotEmpty()
            .WithMessage("ContactChannelId is required.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Type must be a valid ContactChannelType.");

        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Value is required.");
    }
}
