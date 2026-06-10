using Bunit;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.UI.Components.Shared;

using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class PartyStateBadgeTests : BunitContext
{
    public PartyStateBadgeTests() => Services.AddFluentUIComponents();

    [Theory]
    [InlineData(nameof(PartyLifecycleState.Active), "Active", BadgeColor.Success)]
    [InlineData(nameof(PartyLifecycleState.Inactive), "Inactive", BadgeColor.Subtle)]
    [InlineData(nameof(PartyLifecycleState.Restricted), "Restricted", BadgeColor.Warning)]
    [InlineData(nameof(PartyLifecycleState.Erased), "Erased", BadgeColor.Danger)]
    public void Badge_renders_visible_label_and_fluent_tint_for_each_state(
        string stateName,
        string expectedLabel,
        BadgeColor expectedColor)
    {
        PartyLifecycleState state = StateByName(stateName);

        IRenderedComponent<PartyStateBadge> cut = Render<PartyStateBadge>(parameters => parameters
            .Add(component => component.State, state));

        cut.Markup.ShouldContain(expectedLabel);

        FluentBadge badge = cut.FindComponent<FluentBadge>().Instance;
        badge.Appearance.ShouldBe(BadgeAppearance.Tint);
        badge.Shape.ShouldBe(BadgeShape.Rounded);
        badge.Color.ShouldBe(expectedColor);
    }

    [Fact]
    public void Labels_can_be_overridden_without_changing_state_mapping()
    {
        IRenderedComponent<PartyStateBadge> cut = Render<PartyStateBadge>(parameters => parameters
            .Add(component => component.State, PartyLifecycleState.Restricted)
            .Add(component => component.RestrictedLabel, "Limited"));

        cut.Markup.ShouldContain("Limited");
        cut.FindComponent<FluentBadge>().Instance.Color.ShouldBe(BadgeColor.Warning);
    }

    [Fact]
    public void Erased_detail_maps_to_tombstone_copy_without_rendering_personal_data()
    {
        PartyDetail detail = Detail(
            displayName: "Ada Lovelace",
            sortName: "Lovelace, Ada",
            isActive: true,
            isRestricted: true,
            isErased: true) with
        {
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "contact-email",
                    Type = ContactChannelType.Email,
                    Value = "ada@example.test",
                },
            ],
            Identifiers =
            [
                new PartyIdentifier
                {
                    Id = "identifier-tax",
                    Type = IdentifierType.TaxId,
                    Value = "TAX-ADA-1843",
                    Jurisdiction = "GB",
                },
            ],
            NameHistory =
            [
                new NameHistoryEntry
                {
                    DisplayName = "Augusta Ada Byron",
                    SortName = "Byron, Augusta Ada",
                    ChangedAt = DateTimeOffset.UnixEpoch,
                    TriggeredBy = "import",
                },
            ],
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
            },
        };

        IRenderedComponent<PartyStateBadge> cut = Render<PartyStateBadge>(parameters => parameters
            .Add(component => component.State, PartyLifecycleState.FromDetail(detail)));

        cut.Markup.ShouldContain("Erased");
        cut.Markup.ShouldNotContain("Ada Lovelace");
        cut.Markup.ShouldNotContain("Lovelace, Ada");
        cut.Markup.ShouldNotContain(detail.Id);
        cut.Markup.ShouldNotContain("ada@example.test");
        cut.Markup.ShouldNotContain("TAX-ADA-1843");
        cut.Markup.ShouldNotContain("Augusta Ada Byron");
    }

    [Fact]
    public void Detail_factory_applies_erased_then_restricted_then_active_precedence()
    {
        PartyLifecycleState.FromDetail(Detail(isActive: true, isRestricted: true, isErased: true))
            .ShouldBe(PartyLifecycleState.Erased);
        PartyLifecycleState.FromDetail(Detail(isActive: true, isRestricted: true, isErased: false))
            .ShouldBe(PartyLifecycleState.Restricted);
        PartyLifecycleState.FromDetail(Detail(isActive: true, isRestricted: false, isErased: false))
            .ShouldBe(PartyLifecycleState.Active);
        PartyLifecycleState.FromDetail(Detail(isActive: false, isRestricted: false, isErased: false))
            .ShouldBe(PartyLifecycleState.Inactive);
    }

    [Fact]
    public void List_row_factory_does_not_infer_restricted_state()
    {
        PartyLifecycleState.FromListRow(isActive: true, isErased: false).ShouldBe(PartyLifecycleState.Active);
        PartyLifecycleState.FromListRow(isActive: false, isErased: false).ShouldBe(PartyLifecycleState.Inactive);
        PartyLifecycleState.FromListRow(isActive: true, isErased: true).ShouldBe(PartyLifecycleState.Erased);
    }

    private static PartyLifecycleState StateByName(string stateName)
        => stateName switch
        {
            nameof(PartyLifecycleState.Active) => PartyLifecycleState.Active,
            nameof(PartyLifecycleState.Inactive) => PartyLifecycleState.Inactive,
            nameof(PartyLifecycleState.Restricted) => PartyLifecycleState.Restricted,
            nameof(PartyLifecycleState.Erased) => PartyLifecycleState.Erased,
            _ => throw new ArgumentOutOfRangeException(nameof(stateName), stateName, null),
        };

    private static PartyDetail Detail(
        string displayName = "Display",
        string sortName = "Sort",
        bool isActive = true,
        bool isRestricted = false,
        bool isErased = false)
        => new()
        {
            Id = "party-test-id",
            Type = PartyType.Person,
            DisplayName = displayName,
            SortName = sortName,
            IsActive = isActive,
            IsRestricted = isRestricted,
            IsErased = isErased,
        };
}
