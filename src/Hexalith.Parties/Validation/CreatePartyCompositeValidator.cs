using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class CreatePartyCompositeValidator : AbstractValidator<CreatePartyComposite>
{
    public CreatePartyCompositeValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .NotEqual(PartyType.Unknown)
            .WithMessage("Type must be Person or Organization.");

        When(x => x.Type == PartyType.Person, () =>
        {
            RuleFor(x => x.PersonDetails)
                .NotNull()
                .WithMessage("PersonDetails is required when Type is Person.");
        });

        When(x => x.Type == PartyType.Organization, () =>
        {
            RuleFor(x => x.OrganizationDetails)
                .NotNull()
                .WithMessage("OrganizationDetails is required when Type is Organization.");
        });

        RuleFor(x => x)
            .Must(x => CountSubOperations(x) <= PartyAggregate.GetEffectiveMaxSubOperations())
            .WithMessage(x => $"Total sub-operations ({CountSubOperations(x)}) exceeds maximum ({PartyAggregate.GetEffectiveMaxSubOperations()}).");

        RuleFor(x => x)
            .Must(HaveMatchingChildPartyIds)
            .WithMessage("Child PartyId must match PartyId.");

        RuleForEach(x => x.ContactChannels).ChildRules(channel =>
        {
            channel.RuleFor(c => c.PartyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("PartyId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("PartyId must be a support-safe identifier.");

            channel.RuleFor(c => c.ContactChannelId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("ContactChannelId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("ContactChannelId must be a support-safe identifier.");

            channel.RuleFor(c => c.Type)
                .IsInEnum()
                .WithMessage("Type must be a valid ContactChannelType.");

            channel.RuleFor(c => c.Value)
                .NotEmpty()
                .WithMessage("Value is required.");
        });

        RuleForEach(x => x.Identifiers).ChildRules(identifier =>
        {
            identifier.RuleFor(i => i.PartyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("PartyId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("PartyId must be a support-safe identifier.");

            identifier.RuleFor(i => i.IdentifierId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("IdentifierId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("IdentifierId must be a support-safe identifier.");

            identifier.RuleFor(i => i.Type)
                .IsInEnum()
                .WithMessage("Type must be a valid IdentifierType.");

            identifier.RuleFor(i => i.Value)
                .NotEmpty()
                .WithMessage("Value is required.");
        });
    }

    private static int CountSubOperations(CreatePartyComposite command)
        => 1 + command.ContactChannels.Count + command.Identifiers.Count;

    private static bool HaveMatchingChildPartyIds(CreatePartyComposite command)
        => command.ContactChannels.All(channel => channel is not null && string.Equals(channel.PartyId, command.PartyId, StringComparison.Ordinal))
            && command.Identifiers.All(identifier => identifier is not null && string.Equals(identifier.PartyId, command.PartyId, StringComparison.Ordinal));
}
