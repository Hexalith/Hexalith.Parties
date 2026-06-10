using System.Globalization;

using Bunit;

using Hexalith.Parties.ConsumerPortal.Components;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class EditMyProfilePageTests : BunitContext
{
    public EditMyProfilePageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void EditMyProfilePage_Loading_RendersSkeletonBeforeDataResolves()
    {
        var pending = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dataClient = new QueueProfileDataClient(_ => pending.Task);
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());

        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();

        cut.Markup.ShouldContain("Loading editable profile details");
        cut.Markup.ShouldContain("aria-busy=\"true\"");
        cut.FindAll("[role='status']").Count.ShouldBe(1);
        dataClient.CallCount.ShouldBe(1);
    }

    [Fact]
    public void EditMyProfilePage_PersonPrefill_MatchesStoredValuesAndHidesOrganizationFields()
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(CurrentPerson())));
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());

        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "Prefix").Instance.Value.ShouldBe("Dr");
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada");
            FindTextInput(cut, "Last name").Instance.Value.ShouldBe("Lovelace");
            FindTextInput(cut, "Date of birth").Instance.Value.ShouldBe(ExpectedDate(new DateTimeOffset(1815, 12, 10, 0, 0, 0, TimeSpan.Zero)));
            FindTextInput(cut, "Suffix").Instance.Value.ShouldBe("FAS");
            cut.Markup.ShouldContain("ada@example.test");
            cut.Markup.ShouldNotContain("Legal name", Case.Sensitive);
            cut.Markup.ShouldNotContain("party-secret-123", Case.Sensitive);
        });
    }

    [Fact]
    public void EditMyProfilePage_OrganizationPrefill_MatchesStoredValuesAndHidesPersonFields()
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(CurrentOrganization())));
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());

        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "Legal name").Instance.Value.ShouldBe("Hexalith Legal SAS");
            FindTextInput(cut, "Trading name").Instance.Value.ShouldBe("Hexalith");
            FindTextInput(cut, "Legal form").Instance.Value.ShouldBe("SAS");
            FindTextInput(cut, "Registration number").Instance.Value.ShouldBe("FR-ORG-42");
            cut.FindComponent<FluentCheckbox>().Instance.Value.ShouldBeFalse();
            cut.Markup.ShouldNotContain("First name", Case.Sensitive);
            cut.Markup.ShouldNotContain("party-organization-secret", Case.Sensitive);
        });
    }

    [Fact]
    public void EditMyProfilePage_ClientValidation_PreservesTypedInputAndTiesErrorToField()
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(CurrentPerson())));
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));

        SetTextInput(cut, "First name", "Edited");
        SetTextInput(cut, "Last name", string.Empty);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Edited");
            IRenderedComponent<FluentTextInput> lastName = FindTextInput(cut, "Last name");
            lastName.Instance.AdditionalAttributes.ShouldNotBeNull();
            string describedBy = lastName.Instance.AdditionalAttributes!["aria-describedby"].ShouldNotBeNull().ToString()!;
            cut.Find($"#{describedBy.Split(' ').Last()}").TextContent.ShouldContain("This field is required.");
            cut.Find("[role='alert']").TextContent.ShouldContain("Check the highlighted fields and try again.");
            cut.FindAll("[role='alert']").Count.ShouldBe(1);
            cut.Markup.ShouldNotContain("Saving...", Case.Sensitive);
            cut.Markup.ShouldNotContain("Saved - updating", Case.Sensitive);
        });
    }

    [Fact]
    public void EditMyProfilePage_ServerValidation_PreservesTypedInputAndClearsSaveStatus()
    {
        var editClient = new QueueProfileEditClient(_ => Task.FromResult(new ConsumerProfileUpdateResult(
            ConsumerProfileUpdateOutcome.ValidationRejected,
            ValidationFailures:
            [
                new ConsumerProfileValidationFailure("PersonDetails.FirstName"),
            ])));
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(CurrentPerson())));
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));

        SetTextInput(cut, "First name", "Edited");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Edited");
            cut.Find("[role='alert']").TextContent.ShouldContain("Check the highlighted fields and try again.");
            cut.Markup.ShouldNotContain("Saving...", Case.Sensitive);
            editClient.LastRequest.ShouldNotBeNull();
        });
    }

    [Fact]
    public void EditMyProfilePage_AcceptedSave_OptimisticallyUpdatesAndUsesOnePoliteStatusSource()
    {
        var save = new TaskCompletionSource<ConsumerProfileUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        PartyDetail updated = CurrentPerson() with
        {
            DisplayName = "Grace Lovelace",
            PersonDetails = CurrentPerson().PersonDetails! with { FirstName = "Grace" },
            Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
        };
        var dataClient = new QueueProfileDataClient(
            _ => Task.FromResult(CurrentPerson()),
            _ => Task.FromResult(updated));
        var editClient = new QueueProfileEditClient(_ => save.Task);
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));

        SetTextInput(cut, "First name", "Grace");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Saving...");
            cut.FindAll("[role='status'][aria-live='polite']").Count.ShouldBe(1);
        });

        save.SetResult(new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Accepted, updated));

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Grace");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain("Saved");
            editClient.LastRequest.ShouldNotBeNull();
            editClient.LastRequest!.PersonDetails!.FirstName.ShouldBe("Grace");
            dataClient.CallCount.ShouldBe(2);
        });
    }

    [Theory]
    [InlineData(ProjectionFreshnessStatus.Stale, "Showing what we last knew - refreshing")]
    [InlineData(ProjectionFreshnessStatus.Rebuilding, "Showing last known")]
    [InlineData(ProjectionFreshnessStatus.Degraded, "Showing last known")]
    [InlineData(ProjectionFreshnessStatus.Unavailable, "Showing last known")]
    [InlineData(ProjectionFreshnessStatus.LocalOnly, "Showing last known")]
    public void EditMyProfilePage_NonCurrentFreshness_KeepsLastKnownProfileVisible(
        ProjectionFreshnessStatus status,
        string expectedFreshness)
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ =>
            Task.FromResult(CurrentPerson() with { Freshness = ProjectionFreshnessMetadata.Create(status) })));
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());

        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldContain(expectedFreshness);
        });
    }

    [Fact]
    public void EditMyProfilePage_ErasedProfile_RendersTombstoneWithoutPersonalValues()
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(ErasedPerson())));
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());

        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("This profile was deleted");
            cut.Markup.ShouldNotContain("Secret Person", Case.Sensitive);
            cut.Markup.ShouldNotContain("secret@example.test", Case.Sensitive);
            cut.Markup.ShouldNotContain("SECRET-ID", Case.Sensitive);
            cut.Markup.ShouldNotContain("party-erased-secret", Case.Sensitive);
            cut.FindAll("form").ShouldBeEmpty();
        });
    }

    [Fact]
    public void EditMyProfilePage_LoadFailure_RendersGenericAlertAndRetry()
    {
        var dataClient = new QueueProfileDataClient(
            _ => Task.FromException<PartyDetail>(new InvalidOperationException("boom for Ada Lovelace")),
            _ => Task.FromResult(CurrentPerson()));
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='alert']").TextContent.ShouldContain("We couldn't load the editable profile details. Please try again.");
            cut.Markup.ShouldNotContain("boom", Case.Insensitive);
            cut.Markup.ShouldNotContain("Ada Lovelace", Case.Sensitive);
        });

        cut.Find("fluent-button").Click();

        cut.WaitForAssertion(() =>
        {
            dataClient.CallCount.ShouldBe(2);
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada");
        });
    }

    [Fact]
    public void EditMyProfilePage_SaveFailure_RendersGenericAlertAndPreservesInput()
    {
        var editClient = new QueueProfileEditClient(_ => Task.FromResult(new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Failed)));
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(CurrentPerson())));
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));

        SetTextInput(cut, "First name", "Edited");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Edited");
            cut.Find("[role='alert']").TextContent.ShouldContain("We couldn't save the changes. Please try again.");
            cut.Markup.ShouldNotContain("party-secret-123", Case.Sensitive);
        });
    }

    [Fact]
    public void EditMyProfilePage_SaveException_RendersGenericAlertAndPreservesInput()
    {
        var editClient = new QueueProfileEditClient(_ => Task.FromException<ConsumerProfileUpdateResult>(
            new InvalidOperationException("boom for Ada Lovelace party-secret-123")));
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(CurrentPerson())));
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));

        SetTextInput(cut, "First name", "Edited");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Edited");
            cut.Find("[role='alert']").TextContent.ShouldContain("We couldn't save the changes. Please try again.");
            cut.Markup.ShouldNotContain("boom", Case.Insensitive);
            cut.Markup.ShouldNotContain("party-secret-123", Case.Sensitive);
        });
    }

    private static string ExpectedDate(DateTimeOffset date)
        => date.ToString("d", CultureInfo.CurrentCulture);

    private static void SetTextInput(IRenderedComponent<EditMyProfilePage> cut, string label, string value)
    {
        IRenderedComponent<FluentTextInput> input = FindTextInput(cut, label);
        cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync(value)).GetAwaiter().GetResult();
    }

    private static IRenderedComponent<FluentTextInput> FindTextInput(IRenderedComponent<EditMyProfilePage> cut, string label)
        => cut.FindComponents<FluentTextInput>()
            .Single(input => string.Equals(input.Instance.Label, label, StringComparison.Ordinal));

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
                Prefix = "Dr",
                Suffix = "FAS",
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

    private sealed class QueueProfileEditClient(params Func<ConsumerProfileUpdateRequest, Task<ConsumerProfileUpdateResult>>[] calls)
        : IConsumerProfileEditClient
    {
        private int _index;

        public ConsumerProfileUpdateRequest? LastRequest { get; private set; }

        public Task<ConsumerProfileUpdateResult> UpdateMyProfileAsync(
            ConsumerProfileUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            Func<ConsumerProfileUpdateRequest, Task<ConsumerProfileUpdateResult>>? call = calls.Length == 0
                ? null
                : calls[Math.Min(_index, calls.Length - 1)];
            _index++;
            return call is null
                ? Task.FromResult(new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Accepted))
                : call(request);
        }
    }
}
