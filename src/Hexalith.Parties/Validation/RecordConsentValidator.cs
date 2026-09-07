using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class RecordConsentValidator : AbstractValidator<RecordConsent>
{
    public RecordConsentValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.ChannelId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("ChannelId is required.")
            .Must(ConsentIdentifier.IsValidChannelId)
            .WithMessage("ChannelId must be a support-safe identifier.");

        RuleFor(x => x.Purpose)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Purpose is required.")
            .Must(value => ConsentIdentifier.IsValidPurpose(value?.Trim()))
            .WithMessage("Purpose must contain 1 to 100 ASCII alphanumeric characters, hyphens, or underscores.");
    }
}
