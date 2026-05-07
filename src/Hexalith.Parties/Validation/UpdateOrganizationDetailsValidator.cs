using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.Validation;

public sealed class UpdateOrganizationDetailsValidator : AbstractValidator<UpdateOrganizationDetails>
{
    public UpdateOrganizationDetailsValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.OrganizationDetails)
            .NotNull()
            .WithMessage("OrganizationDetails is required.");
    }
}
