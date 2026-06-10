using Bunit;

using Hexalith.Parties.ConsumerPortal.Components;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class MyProfilePageTests : BunitContext
{
    public MyProfilePageTests()
    {
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void MyProfilePage_Loading_RendersCalmSkeleton()
    {
        var pending = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new QueueProfileDataClient(_ => pending.Task);
        Services.AddSingleton<IConsumerProfileDataClient>(client);

        IRenderedComponent<MyProfilePage> cut = Render<MyProfilePage>();

        cut.Markup.ShouldContain("Loading profile details");
        cut.Markup.ShouldContain("aria-busy=\"true\"");
        cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Loading profile details");
        client.CallCount.ShouldBe(1);
    }

    [Fact]
    public void MyProfilePage_CurrentPerson_RendersProfileWithoutInternalIdentifiers()
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(CurrentPerson())));

        IRenderedComponent<MyProfilePage> cut = Render<MyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("h1").TextContent.ShouldBe("My profile");
            cut.Markup.ShouldContain("Review the profile information linked to your signed-in account.");
            cut.Markup.ShouldNotContain("You will be able to review", Case.Sensitive);
            cut.Markup.ShouldContain("Ada Lovelace");
            cut.Markup.ShouldContain("Ada");
            cut.Markup.ShouldContain("Email");
            cut.Markup.ShouldContain("ada@example.test");
            cut.Markup.ShouldContain("Tax ID");
            cut.Markup.ShouldContain("TAX-ADA-1");
            cut.Markup.ShouldContain("Jurisdiction");
            cut.Markup.ShouldContain("FR");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Up to date");
            cut.Markup.ShouldNotContain("party-secret-123", Case.Sensitive);
            cut.Markup.ShouldNotContain("Lovelace, Ada", Case.Sensitive);
        });
    }

    [Fact]
    public void MyProfilePage_CurrentOrganization_RendersOrganizationDetailsAndEmptyStates()
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(CurrentOrganization())));

        IRenderedComponent<MyProfilePage> cut = Render<MyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Hexalith Labs");
            cut.Markup.ShouldContain("Organization details");
            cut.Markup.ShouldContain("Hexalith Legal SAS");
            cut.Markup.ShouldContain("Hexalith");
            cut.Markup.ShouldContain("SAS");
            cut.Markup.ShouldContain("FR-ORG-42");
            cut.Markup.ShouldContain("No contact channels are shown.");
            cut.Markup.ShouldContain("No identifiers are shown.");
            cut.Markup.ShouldContain("No");
            cut.Markup.ShouldNotContain("First name", Case.Sensitive);
            cut.Markup.ShouldNotContain("Last name", Case.Sensitive);
            cut.Markup.ShouldNotContain("party-organization-secret", Case.Sensitive);
        });
    }

    [Theory]
    [InlineData(ProjectionFreshnessStatus.Stale, "Showing what we last knew - refreshing")]
    [InlineData(ProjectionFreshnessStatus.Rebuilding, "Showing last known")]
    [InlineData(ProjectionFreshnessStatus.Degraded, "Showing last known")]
    [InlineData(ProjectionFreshnessStatus.Unavailable, "Showing last known")]
    [InlineData(ProjectionFreshnessStatus.LocalOnly, "Showing last known")]
    public void MyProfilePage_NonCurrentFreshness_KeepsLastKnownContentVisible(
        ProjectionFreshnessStatus status,
        string expectedFreshness)
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ =>
            Task.FromResult(CurrentPerson() with { Freshness = ProjectionFreshnessMetadata.Create(status) })));

        IRenderedComponent<MyProfilePage> cut = Render<MyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Ada Lovelace");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain(expectedFreshness);
        });
    }

    [Fact]
    public void MyProfilePage_NullFreshness_KeepsLastKnownContentVisible()
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ =>
            Task.FromResult(CurrentPerson() with { Freshness = null })));

        IRenderedComponent<MyProfilePage> cut = Render<MyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Ada Lovelace");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Showing last known");
        });
    }

    [Fact]
    public void MyProfilePage_ErasedProfile_RendersTombstoneWithoutPersonalValues()
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(ErasedPerson())));

        IRenderedComponent<MyProfilePage> cut = Render<MyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("This profile was deleted");
            cut.Markup.ShouldContain("Showing last known");
            cut.Markup.ShouldNotContain("Secret Person", Case.Sensitive);
            cut.Markup.ShouldNotContain("secret@example.test", Case.Sensitive);
            cut.Markup.ShouldNotContain("SECRET-ID", Case.Sensitive);
            cut.Markup.ShouldNotContain("party-erased-secret", Case.Sensitive);
        });
    }

    [Fact]
    public void MyProfilePage_LoadFailure_RendersGenericAlertAndRetry()
    {
        var client = new QueueProfileDataClient(
            _ => Task.FromException<PartyDetail>(new InvalidOperationException("boom for Ada Lovelace")),
            _ => Task.FromResult(CurrentPerson()));
        Services.AddSingleton<IConsumerProfileDataClient>(client);

        IRenderedComponent<MyProfilePage> cut = Render<MyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='alert']").TextContent.ShouldContain("We couldn't load your profile details. Please try again.");
            cut.Markup.ShouldNotContain("boom", Case.Insensitive);
            cut.Markup.ShouldNotContain("Ada Lovelace", Case.Sensitive);
        });

        cut.Find("fluent-button").Click();

        cut.WaitForAssertion(() =>
        {
            client.CallCount.ShouldBe(2);
            cut.Markup.ShouldContain("Ada Lovelace");
            cut.Markup.ShouldNotContain("role=\"alert\"", Case.Sensitive);
        });
    }

    private static PartyDetail CurrentPerson()
        => new()
        {
            Id = "party-secret-123",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                DateOfBirth = new DateTimeOffset(1815, 12, 10, 0, 0, 0, TimeSpan.Zero),
            },
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "contact-secret",
                    Type = ContactChannelType.Email,
                    Value = "ada@example.test",
                    IsPreferred = true,
                },
            ],
            Identifiers =
            [
                new PartyIdentifier
                {
                    Id = "identifier-secret",
                    Type = IdentifierType.TaxId,
                    Value = "TAX-ADA-1",
                    Jurisdiction = "FR",
                },
            ],
            CreatedAt = new DateTimeOffset(2026, 06, 01, 0, 0, 0, TimeSpan.Zero),
            LastModifiedAt = new DateTimeOffset(2026, 06, 10, 0, 0, 0, TimeSpan.Zero),
            Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
        };

    private static PartyDetail ErasedPerson()
        => CurrentPerson() with
        {
            Id = "party-erased-secret",
            IsErased = true,
            DisplayName = "Secret Person",
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "erased-contact",
                    Type = ContactChannelType.Email,
                    Value = "secret@example.test",
                    IsPreferred = true,
                },
            ],
            Identifiers =
            [
                new PartyIdentifier
                {
                    Id = "erased-identifier",
                    Type = IdentifierType.Other,
                    Value = "SECRET-ID",
                },
            ],
            Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Degraded),
        };

    private static PartyDetail CurrentOrganization()
        => new()
        {
            Id = "party-organization-secret",
            Type = PartyType.Organization,
            IsActive = true,
            DisplayName = "Hexalith Labs",
            SortName = "Labs, Hexalith",
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Hexalith Legal SAS",
                TradingName = "Hexalith",
                LegalForm = "SAS",
                RegistrationNumber = "FR-ORG-42",
                IsNaturalPerson = false,
            },
            ContactChannels = [],
            Identifiers = [],
            CreatedAt = new DateTimeOffset(2026, 06, 01, 0, 0, 0, TimeSpan.Zero),
            LastModifiedAt = new DateTimeOffset(2026, 06, 10, 0, 0, 0, TimeSpan.Zero),
            Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
        };

    private sealed class QueueProfileDataClient(params Func<CancellationToken, Task<PartyDetail>>[] calls) : IConsumerProfileDataClient
    {
        private int _index;

        public int CallCount { get; private set; }

        public Task<PartyDetail> GetMyPartyAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            Func<CancellationToken, Task<PartyDetail>> call = calls[Math.Min(_index, calls.Length - 1)];
            _index++;
            return call(cancellationToken);
        }
    }
}
