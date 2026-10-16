using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

/// <summary>
/// Validates consent revocation identifiers and optional reasons before command processing.
/// </summary>
public sealed class RevokeConsentValidator : AbstractValidator<RevokeConsent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeConsentValidator"/> class.
    /// </summary>
    public RevokeConsentValidator()
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

        RuleFor(x => x.ConsentId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("ConsentId is required.")
            .Must(ConsentIdentifier.IsValidConsentId)
            .WithMessage("ConsentId must be a support-safe identifier.");

        RuleFor(x => x.Reason)
            .MaximumLength(256)
            .WithMessage("Reason must be no longer than 256 characters.");
    }
}
