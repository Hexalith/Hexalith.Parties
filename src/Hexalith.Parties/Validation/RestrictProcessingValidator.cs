using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.Validation;

public sealed class RestrictProcessingValidator : AbstractValidator<RestrictProcessing>
{
    public const int MaximumReasonLength = 256;

    public RestrictProcessingValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(MaximumReasonLength)
            .WithMessage($"Reason must be present and no longer than {MaximumReasonLength} characters.");
    }
}
