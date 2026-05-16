using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.Validation;

public sealed class UpdateContactChannelValidator : AbstractValidator<UpdateContactChannel>
{
    public UpdateContactChannelValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.ContactChannelId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("ContactChannelId is required.")
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("ContactChannelId must be a valid GUID.");
    }
}
