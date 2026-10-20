using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Domain;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class UpdatePartyCompositeValidator : AbstractValidator<UpdatePartyComposite>
{
    public UpdatePartyCompositeValidator()
    {
        RuleFor(x => x)
            .Must(CompositeInputPolicy.HasBoundedFields)
            .WithMessage("Every input string must be at most 512 UTF-16 code units.")
            .WithErrorCode("CompositeStringLength");

        RuleFor(x => x)
            .Must(CompositeInputPolicy.HasBoundedTotal)
            .WithMessage("Combined input strings must be at most 4096 UTF-16 code units.")
            .WithErrorCode("CompositeTotalLength");

        RuleFor(x => x.AddContactChannels).NotNull();
        RuleForEach(x => x.AddContactChannels).NotNull();

        RuleFor(x => x.UpdateContactChannels).NotNull();
        RuleForEach(x => x.UpdateContactChannels).NotNull();

        RuleFor(x => x.RemoveContactChannelIds).NotNull();
        RuleForEach(x => x.RemoveContactChannelIds).NotNull();

        RuleFor(x => x.AddIdentifiers).NotNull();
        RuleForEach(x => x.AddIdentifiers).NotNull();

        RuleFor(x => x.RemoveIdentifierIds).NotNull();
        RuleForEach(x => x.RemoveIdentifierIds).NotNull();

        RuleFor(x => x)
            .Must(HaveSupportedChange)
            .WithMessage("At least one supported party change is required.")
            .WithErrorCode("CompositeEmptyUpdate");

        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

        RuleFor(x => x)
            .Must(x => CountSubOperations(x) <= PartyAggregate.GetEffectiveMaxSubOperations())
            .WithMessage(x => $"Total sub-operations ({CountSubOperations(x)}) exceeds maximum ({PartyAggregate.GetEffectiveMaxSubOperations()}).");

        RuleFor(x => x)
            .Must(HaveMatchingChildPartyIds)
            .WithMessage("Child PartyId must match PartyId.");

        RuleForEach(x => x.AddContactChannels).ChildRules(channel =>
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

        RuleForEach(x => x.UpdateContactChannels).ChildRules(channel =>
        {
            channel.RuleFor(c => c)
                .Must(c => c.Type is not null || c.Value is not null || c.IsPreferred is not null)
                .WithMessage("A contact-channel update must supply Type, Value or IsPreferred.")
                .WithErrorCode("CompositeEmptyContactUpdate");

            channel.RuleFor(c => c.Type)
                .IsInEnum()
                .When(c => c.Type is not null)
                .WithMessage("Type must be a valid ContactChannelType.");

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
        });

        RuleForEach(x => x.RemoveContactChannelIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("RemoveContactChannelId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("RemoveContactChannelId must be a support-safe identifier.");

        RuleForEach(x => x.AddIdentifiers).ChildRules(identifier =>
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

        RuleForEach(x => x.RemoveIdentifierIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("RemoveIdentifierId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("RemoveIdentifierId must be a support-safe identifier.");
    }

    private static bool HaveSupportedChange(UpdatePartyComposite command)
        => command.PersonDetails is not null || command.OrganizationDetails is not null
            || (command.AddContactChannels?.Count ?? 0) > 0
            || (command.RemoveContactChannelIds?.Count ?? 0) > 0
            || (command.AddIdentifiers?.Count ?? 0) > 0
            || (command.RemoveIdentifierIds?.Count ?? 0) > 0
            || (command.UpdateContactChannels?.Any(c => c is not null && (c.Type is not null || c.Value is not null || c.IsPreferred is not null)) ?? false);

    private static int CountSubOperations(UpdatePartyComposite command)
        => (command.PersonDetails is not null ? 1 : 0)
            + (command.OrganizationDetails is not null ? 1 : 0)
            + (command.AddContactChannels?.Count ?? 0)
            + (command.UpdateContactChannels?.Count ?? 0)
            + (command.RemoveContactChannelIds?.Count ?? 0)
            + (command.AddIdentifiers?.Count ?? 0)
            + (command.RemoveIdentifierIds?.Count ?? 0);

    private static bool HaveMatchingChildPartyIds(UpdatePartyComposite command)
        => command.AddContactChannels is not null && command.AddContactChannels.All(channel => channel is not null && string.Equals(channel.PartyId, command.PartyId, StringComparison.Ordinal))
            && command.UpdateContactChannels is not null && command.UpdateContactChannels.All(channel => channel is not null && string.Equals(channel.PartyId, command.PartyId, StringComparison.Ordinal))
            && command.AddIdentifiers is not null && command.AddIdentifiers.All(identifier => identifier is not null && string.Equals(identifier.PartyId, command.PartyId, StringComparison.Ordinal));
}
