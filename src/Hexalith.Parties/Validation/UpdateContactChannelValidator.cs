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
            .NotEmpty()
            .WithMessage("ContactChannelId is required.");
    }
}
