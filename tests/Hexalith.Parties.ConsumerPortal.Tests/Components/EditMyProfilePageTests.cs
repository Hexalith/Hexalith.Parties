using System.Diagnostics;
using System.Globalization;

using Bunit;

using Hexalith.Parties.ConsumerPortal.Components;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class EditMyProfilePageTests : ConsumerPortalBunitContext
{
    public EditMyProfilePageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Theory]
    [InlineData("Static", false)]
    [InlineData("Server", true)]
    public void EditMyProfilePage_RendererInteractivity_GatesFieldsetAndSave(string rendererName, bool isInteractive)
    {
        PageRenderer = new RendererInfo(rendererName, isInteractive);
        var editClient = new QueueProfileEditClient();
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(_ => Task.FromResult(CurrentPerson())));
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);

        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            // Prerendered values stay visible; only the interactive circuit may accept edits or a submit.
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada");
            cut.Find("fieldset").HasAttribute("disabled").ShouldBe(!isInteractive);
            foreach (string action in new[] { "Save changes", "Cancel" })
            {
                // Cancel sits in the gated fieldset too, so its own state must agree with the native one.
                AngleSharp.Dom.IElement button = cut.FindAll("fluent-button")
                    .Single(item => item.TextContent.Trim() == action);
                button.HasAttribute("disabled").ShouldBe(!isInteractive);
                button.GetAttribute("aria-disabled").ShouldBe(isInteractive ? "false" : "true");
            }

            IRenderedComponent<FluentButton>[] actions = [.. cut.FindComponents<FluentButton>()
                .Where(button => button.Instance.Type is ButtonType.Submit or ButtonType.Button)];
            actions.Length.ShouldBe(2);
            actions.ShouldAllBe(button => button.Instance.Disabled == !isInteractive);
        });
        editClient.LastRequest.ShouldBeNull();
    }

    [Theory]
    [InlineData("Static", false)]
    [InlineData("Server", true)]
    public void EditMyProfilePage_RendererInteractivity_GatesLoadFailureRetry(string rendererName, bool isInteractive)
    {
        PageRenderer = new RendererInfo(rendererName, isInteractive);
        var dataClient = new QueueProfileDataClient(
            _ => Task.FromException<PartyDetail>(new InvalidOperationException("load failure")),
            _ => Task.FromResult(CurrentPerson()));
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());

        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='alert']").TextContent.ShouldContain("We couldn't load the editable profile details.");
            // A prerendered Retry has no listener until the circuit renders interactively; its click would be lost.
            AngleSharp.Dom.IElement retry = cut.FindAll("fluent-button").ShouldHaveSingleItem();
            retry.HasAttribute("disabled").ShouldBe(!isInteractive);
            retry.GetAttribute("aria-disabled").ShouldBe(isInteractive ? "false" : "true");
        });
        dataClient.CallCount.ShouldBe(1);
        if (isInteractive)
        {
            cut.Find("fluent-button").Click();
            cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));
            dataClient.CallCount.ShouldBe(2);
        }
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
            cut.FindComponents<FluentAccordion>().Count.ShouldBe(1);
            cut.FindComponents<FluentAccordionItem>().Count.ShouldBe(4);
            cut.FindComponents<FluentAccordionItem>().All(static item => item.Instance.Expanded).ShouldBeTrue();
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
    public async Task EditMyProfilePage_AcceptedSave_OptimisticallyUpdatesAndUsesOnePoliteStatusSource()
    {
        var save = new TaskCompletionSource<ConsumerProfileUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readback = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readbackStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dataClient = new QueueProfileDataClient(
            _ => Task.FromResult(CurrentPerson()),
            token =>
            {
                readbackStarted.SetResult(token);
                return readback.Task;
            });
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

        PartyDetail updated = CurrentPerson() with
        {
            DisplayName = "Grace Lovelace",
            PersonDetails = editClient.LastRequest.ShouldNotBeNull().PersonDetails,
            Identifiers = [new PartyIdentifier { Id = "accepted", Type = IdentifierType.Other, Value = "ACCEPTED-ID" }],
        };
        save.SetResult(new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Accepted, updated));
        CancellationToken readbackToken = await readbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Grace Lovelace");
            cut.Markup.ShouldContain("ACCEPTED-ID");
            cut.Find("fieldset").HasAttribute("disabled").ShouldBeFalse();
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved - updating");
            cut.FindAll("[role='status'][aria-live='polite']").Count.ShouldBe(1);
            dataClient.CallCount.ShouldBe(2);
            readbackToken.CanBeCanceled.ShouldBeTrue();
            readbackToken.IsCancellationRequested.ShouldBeFalse();
        });
        ConsumerProfileUpdateRequest accepted = editClient.LastRequest.ShouldNotBeNull();
        await cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        editClient.LastRequest.ShouldBeSameAs(accepted);
        readbackToken.IsCancellationRequested.ShouldBeFalse();
        dataClient.CallCount.ShouldBe(2);
        cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved - updating");
        readback.SetResult(updated);

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Grace");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved");
            editClient.LastRequest.ShouldNotBeNull();
            editClient.LastRequest!.PersonDetails!.FirstName.ShouldBe("Grace");
            dataClient.CallCount.ShouldBe(2);
        });
    }

    [Fact]
    public async Task EditMyProfilePage_LateAcceptanceAfterDisposalDoesNotReadBack()
    {
        var save = new TaskCompletionSource<ConsumerProfileUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dataClient = new QueueProfileDataClient(_ => Task.FromResult(CurrentPerson()));
        var editClient = new QueueProfileEditClient(_ => save.Task);
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));
        SetTextInput(cut, "First name", "Grace");
        Task submit = cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        cut.WaitForAssertion(() => editClient.LastRequest.ShouldNotBeNull());

        await DisposeComponentsAsync();
        editClient.LastToken.IsCancellationRequested.ShouldBeTrue();
        save.SetResult(new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Accepted));
        await submit.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        dataClient.CallCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false, ProjectionFreshnessStatus.Stale)]
    [InlineData(false, ProjectionFreshnessStatus.Current)]
    [InlineData(true, ProjectionFreshnessStatus.Stale)]
    [InlineData(true, ProjectionFreshnessStatus.Current)]
    public void EditMyProfilePage_ReadbackOfEarlierDetailsKeepsAcceptedEdits(bool organization, ProjectionFreshnessStatus freshness)
    {
        PartyDetail original = organization ? CurrentOrganization() : CurrentPerson();
        const string edited = "Accepted edit";
        string label = organization ? "Legal name" : "First name";
        var dataClient = new QueueProfileDataClient(
            _ => Task.FromResult(original),
            _ => Task.FromResult(original with { Freshness = ProjectionFreshnessMetadata.Create(freshness) }));
        var editClient = new QueueProfileEditClient(request => Task.FromResult(new ConsumerProfileUpdateResult(
            ConsumerProfileUpdateOutcome.Accepted, organization ? original with
            {
                DisplayName = edited, OrganizationDetails = request.OrganizationDetails,
            } : null)));
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, label).Instance.Value.ShouldNotBeEmpty());

        SetTextInput(cut, label, edited);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            dataClient.CallCount.ShouldBe(3);
            cut.Find("fieldset").HasAttribute("disabled").ShouldBeFalse();
            FindTextInput(cut, label).Instance.Value.ShouldBe(edited);
            cut.Markup.ShouldContain(edited);
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved - showing the latest available details");
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditMyProfilePage_OlderAcceptanceAndInconclusiveReadbacksKeepSubmittedDetails(bool organization)
    {
        PartyDetail original = organization ? CurrentOrganization() : CurrentPerson();
        string label = organization ? "Legal name" : "First name";
        const string edited = "Accepted edit";
        const string acceptedIdentifier = "ACCEPTED-READ-ONLY-ID";
        PartyDetail accepted = original with
        {
            Identifiers = [new PartyIdentifier { Id = "accepted", Type = IdentifierType.Other, Value = acceptedIdentifier }],
        };
        var firstReadback = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dataClient = new QueueProfileDataClient(
            _ => Task.FromResult(original),
            _ => firstReadback.Task,
            _ => Task.FromResult(original with { Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Stale) }));
        var editClient = new QueueProfileEditClient(_ => Task.FromResult(new ConsumerProfileUpdateResult(
            ConsumerProfileUpdateOutcome.Accepted, accepted)));
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, label).Instance.Value.ShouldNotBeEmpty());

        SetTextInput(cut, label, edited);
        Task submit = cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);

        cut.WaitForAssertion(() =>
        {
            dataClient.CallCount.ShouldBe(2);
            FindTextInput(cut, label).Instance.Value.ShouldBe(edited);
            cut.Markup.ShouldContain(edited);
            cut.Markup.ShouldContain(acceptedIdentifier);
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved - updating");
        });

        firstReadback.SetResult(original);
        await submit.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() =>
        {
            dataClient.CallCount.ShouldBe(3);
            FindTextInput(cut, label).Instance.Value.ShouldBe(edited);
            cut.Markup.ShouldContain(edited);
            cut.Markup.ShouldContain(acceptedIdentifier);
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved - showing the latest available details");
        });
    }

    [Theory]
    [InlineData(false, ProjectionFreshnessStatus.Stale, true)]
    [InlineData(true, ProjectionFreshnessStatus.Stale, true)]
    [InlineData(false, ProjectionFreshnessStatus.Current, true)]
    [InlineData(true, ProjectionFreshnessStatus.Current, true)]
    [InlineData(false, ProjectionFreshnessStatus.Current, false)]
    [InlineData(true, ProjectionFreshnessStatus.Current, false)]
    public void EditMyProfilePage_ReadbackConfirmsOnlyCurrentMatchingDetails(bool organization, ProjectionFreshnessStatus freshness, bool matching)
    {
        PartyDetail original = organization ? CurrentOrganization() : CurrentPerson();
        const string edited = "Edited details";
        string label = organization ? "Trading name" : "Prefix";
        var editClient = new QueueProfileEditClient();
        var dataClient = new QueueProfileDataClient(_ => Task.FromResult(original), _ =>
        {
            ConsumerProfileUpdateRequest submitted = editClient.LastRequest.ShouldNotBeNull();
            return Task.FromResult(original with
            {
                PersonDetails = matching || organization ? submitted.PersonDetails
                    : submitted.PersonDetails! with { Prefix = original.PersonDetails!.Prefix },
                OrganizationDetails = matching || !organization ? submitted.OrganizationDetails
                    : submitted.OrganizationDetails! with { TradingName = original.OrganizationDetails!.TradingName },
                Freshness = ProjectionFreshnessMetadata.Create(freshness),
            });
        });
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, label).Instance.Value.ShouldNotBeEmpty());

        SetTextInput(cut, label, edited);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            bool confirmed = matching && freshness == ProjectionFreshnessStatus.Current;
            dataClient.CallCount.ShouldBe(confirmed ? 2 : 3);
            cut.Find("fieldset").HasAttribute("disabled").ShouldBeFalse();
            FindTextInput(cut, label).Instance.Value.ShouldBe(edited);
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe(confirmed
                ? "Saved" : "Saved - showing the latest available details");
        });
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    public void EditMyProfilePage_HonorsErasureOrChangedProfileWithoutConfirmingSave(bool erased, bool delayed, bool fromAcceptance)
    {
        PartyDetail original = CurrentPerson();
        PartyDetail changed = original with
        {
            Id = erased ? original.Id : "replacement-profile",
            IsErased = erased,
            DisplayName = "Replacement profile",
            PersonDetails = original.PersonDetails! with { FirstName = "Replacement" },
        };
        var dataClient = new QueueProfileDataClient(
            _ => Task.FromResult(original), _ => Task.FromResult(delayed ? original : changed), _ => Task.FromResult(changed));
        var editClient = new QueueProfileEditClient(_ => Task.FromResult(new ConsumerProfileUpdateResult(
            ConsumerProfileUpdateOutcome.Accepted, fromAcceptance ? changed : null)));
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));

        SetTextInput(cut, "First name", "Grace");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            if (erased)
            {
                cut.FindAll("form").ShouldBeEmpty();
                cut.Markup.ShouldNotContain("Grace");
            }
            else
            {
                cut.Markup.ShouldContain("Replacement profile");
                FindTextInput(cut, "First name").Instance.Value.ShouldBe("Replacement");
            }

            cut.Markup.ShouldNotContain("Saved");
            dataClient.CallCount.ShouldBe(fromAcceptance ? 1 : delayed ? 3 : 2);
        });
    }

    [Theory]
    [InlineData(ProjectionFreshnessStatus.Stale)]
    [InlineData(ProjectionFreshnessStatus.Current)]
    public async Task EditMyProfilePage_DelayedReadbackConfirmsSaveAndPreservesInterveningEdits(ProjectionFreshnessStatus firstFreshness)
    {
        var delayed = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedStarted = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        long firstRead = 0;
        PartyDetail original = CurrentPerson();
        var editClient = new QueueProfileEditClient();
        var dataClient = new QueueProfileDataClient(_ => Task.FromResult(original), _ =>
        {
            firstRead = Stopwatch.GetTimestamp();
            return Task.FromResult(original with
            {
                // The stale result matches the submitted values. The current result still has the old values.
                PersonDetails = firstFreshness == ProjectionFreshnessStatus.Stale ? editClient.LastRequest!.PersonDetails : original.PersonDetails,
                Freshness = ProjectionFreshnessMetadata.Create(firstFreshness),
            });
        }, _ =>
        {
            delayedStarted.SetResult(Stopwatch.GetElapsedTime(firstRead));
            return delayed.Task;
        });
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));
        SetTextInput(cut, "First name", "Grace");
        Task submit = cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        TimeSpan retryDelay = await delayedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        retryDelay.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(200));

        cut.Find("fieldset").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved - updating");
        cut.Markup.ShouldNotContain("PROJECTED-ID");
        SetTextInput(cut, "First name", "Unsaved draft");
        delayed.SetResult(original with
        {
            DisplayName = "Grace Lovelace",
            PersonDetails = editClient.LastRequest.ShouldNotBeNull().PersonDetails,
            Identifiers = [new PartyIdentifier { Id = "projected", Type = IdentifierType.Other, Value = "PROJECTED-ID" }],
        });
        await submit.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Unsaved draft");
            cut.Markup.ShouldContain("Grace Lovelace");
            cut.Markup.ShouldContain("PROJECTED-ID");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved");
            dataClient.CallCount.ShouldBe(3);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditMyProfilePage_TransientFirstReadbackRetriesUnlessDisposed(bool dispose)
    {
        CancellationToken token = Xunit.TestContext.Current.CancellationToken;
        var firstRead = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRead = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PartyDetail original = CurrentPerson();
        int submissions = 0;
        var editClient = new QueueProfileEditClient(_ =>
        {
            submissions++;
            return Task.FromResult(new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Accepted));
        });
        var dataClient = new QueueProfileDataClient(_ => Task.FromResult(original), _ =>
        {
            firstStarted.SetResult();
            return firstRead.Task;
        }, _ =>
        {
            secondStarted.SetResult();
            return secondRead.Task;
        });
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));
        SetTextInput(cut, "First name", "Grace");
        Task submit = cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        cut.Find("fieldset").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved - updating");

        firstRead.SetException(new HttpRequestException("temporary projection failure"));
        if (dispose)
        {
            await cut.InvokeAsync(cut.Instance.Dispose);
            await submit.WaitAsync(TimeSpan.FromSeconds(5), token);
            dataClient.CallCount.ShouldBe(2);
            submissions.ShouldBe(1);
            return;
        }

        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        SetTextInput(cut, "First name", "Unsaved draft");
        secondRead.SetResult(original with
        {
            DisplayName = "Grace Lovelace",
            PersonDetails = editClient.LastRequest.ShouldNotBeNull().PersonDetails,
        });
        await submit.WaitAsync(TimeSpan.FromSeconds(5), token);
        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Unsaved draft");
            cut.Markup.ShouldContain("Grace Lovelace");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved");
            dataClient.CallCount.ShouldBe(3);
            submissions.ShouldBe(1);
        });
    }

    [Fact]
    public async Task EditMyProfilePage_StalledDelayedReadbackTimesOutAndIgnoresLateResult()
    {
        var delayed = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        PartyDetail original = CurrentPerson();
        var dataClient = new QueueProfileDataClient(_ => Task.FromResult(original), _ => Task.FromResult(original), token =>
        {
            delayedStarted.SetResult(token);
            return delayed.Task;
        });
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));
        SetTextInput(cut, "First name", "Grace");
        Task submit = cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        CancellationToken delayedToken = await delayedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        await submit.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        delayedToken.IsCancellationRequested.ShouldBeTrue();

        delayed.SetResult(original with { IsErased = true });
        cut.WaitForAssertion(() =>
        {
            cut.Find("fieldset").HasAttribute("disabled").ShouldBeFalse();
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Grace");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved - showing the latest available details");
            dataClient.CallCount.ShouldBe(3);
        });
    }

    [Fact]
    public async Task EditMyProfilePage_NewerSubmissionSupersedesDelayedReadback()
    {
        var delayed = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newerSave = new TaskCompletionSource<ConsumerProfileUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        PartyDetail original = CurrentPerson();
        var editClient = new QueueProfileEditClient(
            _ => Task.FromResult(new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Accepted)), _ => newerSave.Task);
        var dataClient = new QueueProfileDataClient(_ => Task.FromResult(original), _ => Task.FromResult(original), token =>
        {
            delayedStarted.SetResult(token);
            return delayed.Task;
        }, _ => Task.FromResult(original with
        {
            DisplayName = "Katherine Lovelace", PersonDetails = editClient.LastRequest.ShouldNotBeNull().PersonDetails,
        }));
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));
        SetTextInput(cut, "First name", "Grace");
        Task first = cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        CancellationToken delayedToken = await delayedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        CancellationToken firstToken = editClient.LastToken;
        SetTextInput(cut, "First name", "Katherine");
        Task second = cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        cut.WaitForAssertion(() => cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saving..."));

        firstToken.IsCancellationRequested.ShouldBeTrue();
        delayedToken.IsCancellationRequested.ShouldBeTrue();
        delayed.SetResult(original with { IsErased = true });
        await first.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        cut.Find("fieldset").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saving...");
        newerSave.SetResult(new ConsumerProfileUpdateResult(ConsumerProfileUpdateOutcome.Accepted));
        await second.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Katherine");
            cut.Markup.ShouldContain("Katherine Lovelace");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved");
            dataClient.CallCount.ShouldBe(4);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditMyProfilePage_InvalidResubmissionKeepsAcceptedSaveObservation(bool erased)
    {
        var delayed = new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        PartyDetail original = CurrentPerson();
        var editClient = new QueueProfileEditClient();
        var dataClient = new QueueProfileDataClient(_ => Task.FromResult(original), _ => Task.FromResult(original), token =>
        {
            delayedStarted.SetResult(token);
            return delayed.Task;
        });
        Services.AddSingleton<IConsumerProfileDataClient>(dataClient);
        Services.AddSingleton<IConsumerProfileEditClient>(editClient);
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));
        SetTextInput(cut, "First name", "Grace");
        Task accepted = cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        CancellationToken delayedToken = await delayedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        CancellationToken saveToken = editClient.LastToken;
        ConsumerProfileUpdateRequest submitted = editClient.LastRequest.ShouldNotBeNull();

        SetTextInput(cut, "First name", string.Empty);
        await cut.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);

        cut.Find("[role='alert']").TextContent.ShouldContain("Check the highlighted fields and try again.");
        editClient.LastRequest.ShouldBeSameAs(submitted);
        saveToken.IsCancellationRequested.ShouldBeFalse();
        delayedToken.IsCancellationRequested.ShouldBeFalse();
        delayed.SetResult(original with { IsErased = erased, PersonDetails = submitted.PersonDetails });
        await accepted.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() =>
        {
            if (erased)
            {
                cut.FindAll("form").ShouldBeEmpty();
            }
            else
            {
                FindTextInput(cut, "First name").Instance.Value.ShouldBeEmpty();
                cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved");
            }

            dataClient.CallCount.ShouldBe(3);
        });
    }

    [Fact]
    public void EditMyProfilePage_AcceptedSaveRetainsSubmittedDetailsAndReadErrorStatus()
    {
        Services.AddSingleton<IConsumerProfileDataClient>(new QueueProfileDataClient(
            _ => Task.FromResult(CurrentPerson()),
            _ => Task.FromException<PartyDetail>(new InvalidOperationException("readback failure"))));
        Services.AddSingleton<IConsumerProfileEditClient>(new QueueProfileEditClient());
        IRenderedComponent<EditMyProfilePage> cut = Render<EditMyProfilePage>();
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));

        SetTextInput(cut, "First name", "Grace");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Grace");
            cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe("Saved - showing the latest available details");
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

        public CancellationToken LastToken { get; private set; }

        public Task<ConsumerProfileUpdateResult> UpdateMyProfileAsync(
            ConsumerProfileUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
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
