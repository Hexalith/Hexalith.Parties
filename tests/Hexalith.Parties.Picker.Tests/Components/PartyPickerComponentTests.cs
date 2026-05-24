using System.Text.Json;

using AngleSharp.Dom;

using Bunit;

using Hexalith.Parties.Client;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Picker.Components;
using Hexalith.Parties.Picker.Services;
using Hexalith.Parties.Picker.Tests.Fakes;
using Hexalith.Parties.Picker.Tests.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.Picker.Tests.Components;

public sealed class PartyPickerComponentTests : BunitContext
{
    [Fact]
    public void PartyPicker_InitialRender_ExposesAccessibleSearchAndIdleState()
    {
        RegisterClient();

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").GetAttribute("aria-label").ShouldBe("Search parties");
        cut.Find("input").GetAttribute("role").ShouldBe("combobox");
        cut.Find("input").GetAttribute("aria-haspopup").ShouldBe("listbox");
        cut.Markup.ShouldContain("role=\"status\"");
        cut.FindAll("[role=\"listbox\"]").ShouldBeEmpty();
    }

    [Fact]
    public void PartyPicker_UsesLocalizedLabelsWithoutUnsafeMarkup()
    {
        RegisterClient();

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.Labels, new PartyPickerLabels
            {
                SearchLabel = "Rechercher des tiers",
                Placeholder = "Nom du tiers",
            })
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").GetAttribute("aria-label").ShouldBe("Rechercher des tiers");
        cut.Find("input").GetAttribute("placeholder").ShouldBe("Nom du tiers");
    }

    [Fact]
    public void PartyPicker_LocalizedLabels_RenderThroughEncodedTextAndAttributePaths()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result(name: "Ada Lovelace")));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.Labels, new PartyPickerLabels
            {
                SearchLabel = "<img src=x onerror=alert(1)>",
                Placeholder = "<svg onload=alert(2)>",
                Results = "<script>results()</script>",
                Active = "<b>Active</b>",
            })
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").GetAttribute("aria-label").ShouldBe("<img src=x onerror=alert(1)>");
        cut.Find("input").GetAttribute("placeholder").ShouldBe("<svg onload=alert(2)>");
        cut.Markup.ShouldContain("&lt;img src=x onerror=alert(1)&gt;");
        cut.FindAll("img").ShouldBeEmpty();
        cut.FindAll("svg").ShouldBeEmpty();

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role=\"listbox\"]").GetAttribute("aria-label").ShouldBe("<script>results()</script>");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("<b>Active</b>");
            cut.Markup.ShouldContain("&lt;b&gt;Active&lt;/b&gt;");
            cut.FindAll("script").ShouldBeEmpty();
            cut.FindAll(".hx-party-picker__badge b").ShouldBeEmpty();
        });
    }

    [Fact]
    public void PartyPicker_SearchResults_ExposeAccessibleRelationshipsAndLocalizedOptionText()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            PartyPickerTestData.Result(id: "party-1", name: "Ada Lovelace", type: PartyType.Person, active: true),
            PartyPickerTestData.Result(id: "party-2", name: "Contoso Ltd", type: PartyType.Organization, active: false)));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.Labels, new PartyPickerLabels
            {
                SearchLabel = "Chercher des tiers",
                Placeholder = "Nom du tiers",
                Results = "Resultats",
                Active = "Actif",
                Inactive = "Inactif",
                PersonType = "Personne",
                OrganizationType = "Organisation",
            })
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            IElement input = cut.Find("input");
            IElement status = cut.Find(".hx-party-picker__status");
            IElement list = cut.Find("[role=\"listbox\"]");
            IReadOnlyList<IElement> options = cut.FindAll("[role=\"option\"]");

            input.GetAttribute("aria-describedby").ShouldBe(status.Id);
            status.GetAttribute("aria-atomic").ShouldBe("true");
            list.GetAttribute("aria-label").ShouldBe("Resultats");
            list.GetAttribute("aria-describedby").ShouldBe(status.Id);
            options.Count.ShouldBe(2);
            options[0].TextContent.ShouldContain("Personne");
            options[0].TextContent.ShouldContain("Actif");
            options[0].GetAttribute("aria-selected").ShouldBe("false");
            options[0].GetAttribute("aria-describedby").ShouldNotBeNullOrWhiteSpace();
            options[1].TextContent.ShouldContain("Organisation");
            options[1].TextContent.ShouldContain("Inactif");
        });
    }

    [Fact]
    public async Task PartyPicker_SearchResults_UseNativeKeyboardControlsAndExposeSelectedAndErasedState()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            PartyPickerTestData.Result(id: "party-1", name: "Ada Lovelace", type: PartyType.Person, active: true),
            PartyPickerTestData.Result(id: "party-2", name: "Retired Org", type: PartyType.Organization, erased: true)));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("retired");

        cut.WaitForAssertion(() =>
        {
            IElement input = cut.Find("input");
            IElement clear = cut.Find(".hx-party-picker__icon-button");
            IReadOnlyList<IElement> options = cut.FindAll("button[role=\"option\"]");

            input.GetAttribute("aria-autocomplete").ShouldBe("list");
            input.GetAttribute("aria-expanded").ShouldBe("true");
            clear.GetAttribute("type").ShouldBe("button");
            clear.HasAttribute("disabled").ShouldBeFalse();
            options.Count.ShouldBe(2);
            options.ShouldAllBe(option => option.GetAttribute("type") == "button");
            options.ShouldAllBe(option => !option.HasAttribute("tabindex"));
            options.ShouldAllBe(option => !option.HasAttribute("disabled"));

            string? erasedDescriptionId = options[1].GetAttribute("aria-describedby");
            erasedDescriptionId.ShouldNotBeNullOrWhiteSpace();
            cut.Find($"#{erasedDescriptionId}").TextContent.ShouldBe("Erased");
            options[1].TextContent.ShouldContain("Retired Org");
            options[1].TextContent.ShouldContain("Organization");
            options[1].TextContent.ShouldContain("Erased");
        });

        await cut.InvokeAsync(() => cut.FindAll("button[role=\"option\"]")[1].Click());

        cut.WaitForAssertion(() =>
        {
            IReadOnlyList<IElement> options = cut.FindAll("button[role=\"option\"]");
            options[0].GetAttribute("aria-selected").ShouldBe("false");
            options[1].GetAttribute("aria-selected").ShouldBe("true");
            cut.Find(".hx-party-picker__selected").GetAttribute("role").ShouldBe("group");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("Erased");
        });
    }

    [Fact]
    public void PartyPicker_LocalizedLabels_DriveStatusCountsRetrySelectionAndStateText()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            7,
            PartyPickerTestData.Result(id: "party-1", name: "Ada Lovelace", type: PartyType.Person, active: false)));
        RegisterClient(queryClient);

        PartyPickerLabels labels = new()
        {
            SearchLabel = "L-Search",
            Placeholder = "L-Placeholder",
            Loading = "L-Loading",
            Idle = "L-Idle",
            Results = "L-Results",
            ResultsSummary = "L-Count {0}/{1}",
            VisibleResultsSummary = "L-Visible {0}",
            TransientFailure = "L-Transient",
            Selected = "L-Selected",
            ClearSelection = "L-Clear",
            Retry = "L-Retry",
            Inactive = "L-Inactive",
            PersonType = "L-Person",
        };

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.Labels, labels)
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").GetAttribute("placeholder").ShouldBe("L-Placeholder");
        cut.Find(".hx-party-picker__status").TextContent.ShouldBe("L-Idle");

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe("L-Count 1/7");
            cut.Find("[role=\"option\"]").TextContent.ShouldContain("L-Person");
            cut.Find("[role=\"option\"]").TextContent.ShouldContain("L-Inactive");
        });

        cut.Find("[role=\"option\"]").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__selected").GetAttribute("aria-label").ShouldBe("L-Selected");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("L-Inactive");
            cut.Find(".hx-party-picker__icon-button").GetAttribute("aria-label").ShouldBe("L-Clear");
        });

        queryClient.ThrowOnSearch = new HttpRequestException("raw backend token");
        cut.Find("input").Input("retry");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe("L-Transient");
            cut.Find(".hx-party-picker__retry").TextContent.ShouldBe("L-Retry");
            cut.Markup.ShouldNotContain("raw backend token");
        });
    }

    [Fact]
    public void PartyPickerLabels_AllDefaultsArePresentForLocalizationFallback()
    {
        var labels = new PartyPickerLabels();

        foreach (System.Reflection.PropertyInfo property in typeof(PartyPickerLabels).GetProperties())
        {
            if (property.PropertyType == typeof(string))
            {
                string? value = (string?)property.GetValue(labels);
                value.ShouldNotBeNullOrWhiteSpace(property.Name);
            }
        }
    }

    [Fact]
    public void PartyPicker_SearchSuccess_RendersEncodedResultsAndStatusText()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result(name: "<script>alert(1)</script>")));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("script");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("&lt;script&gt;alert(1)&lt;/script&gt;");
            cut.FindAll("[role=\"option\"]").Count.ShouldBe(1);
        });
    }

    [Fact]
    public void PartyPicker_RapidInput_CoalescesToCurrentDebouncedQuery()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result(name: "Ada Lovelace")));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 50)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("a");
        cut.Find("input").Input("ad");
        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            queryClient.SearchCalls.ShouldBe([new SearchCall("ada", 1, PartyPickerDefaults.PageSize)]);
            cut.FindAll("[role=\"option\"]").Count.ShouldBe(1);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void PartyPicker_EmptyOrWhitespaceQuery_DoesNotShowLoadingOrCallClient(string query)
    {
        var queryClient = new RecordingPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input(query);

        cut.Find(".hx-party-picker__status").TextContent.ShouldBe("Enter a party name to search");
        cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
        queryClient.SearchCalls.ShouldBeEmpty();
    }

    [Fact]
    public void PartyPicker_InvisibleOnlyQuery_DoesNotShowLoadingOrCallClient()
    {
        var queryClient = new RecordingPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("\u0000\u0001");

        cut.Find(".hx-party-picker__status").TextContent.ShouldBe("Enter a party name to search");
        cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
        queryClient.SearchCalls.ShouldBeEmpty();
    }

    [Fact]
    public void PartyPicker_SearchSuccess_RendersBoundedResultCountFromSafeMetadata()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            42,
            PartyPickerTestData.Result(id: "party-1", name: "Ada Lovelace"),
            PartyPickerTestData.Result(id: "party-2", name: "Ada Byron")));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe("Showing 2 of 42 matching parties");
            cut.FindAll("[role=\"option\"]").Count.ShouldBe(2);
        });
    }

    [Fact]
    public void PartyPicker_SearchSuccess_WithInconsistentCountRendersVisibleCountOnly()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            -1,
            PartyPickerTestData.Result(id: "party-1", name: "Ada Lovelace"),
            PartyPickerTestData.Result(id: "party-2", name: "Ada Byron")));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            string statusText = cut.Find(".hx-party-picker__status").TextContent;
            statusText.ShouldBe("Showing 2 matching parties");
            statusText.ShouldNotContain("-1");
            cut.FindAll("[role=\"option\"]").Count.ShouldBe(2);
        });
    }

    [Fact]
    public void PartyPicker_SearchSuccess_RendersOnlyBoundedPageResults()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            3,
            PartyPickerTestData.Result(id: "party-1", name: "Ada One"),
            PartyPickerTestData.Result(id: "party-2", name: "Ada Two"),
            PartyPickerTestData.Result(id: "party-3", name: "Ada Three")));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.PageSize, 2)
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            queryClient.SearchCalls.ShouldBe([new SearchCall("ada", 1, 2)]);
            cut.FindAll("[role=\"option\"]").Count.ShouldBe(2);
            cut.Markup.ShouldNotContain("Ada Three");
        });
    }

    [Fact]
    public void PartyPicker_SearchNoResults_RendersAuthorizedContextEmptyState()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage());
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("missing");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe("No matching parties in the current authorized context");
            cut.Markup.ShouldNotContain("tenant", Case.Insensitive);
            cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task PartyPicker_NewSearch_ShowsLoadingAndHidesPreviousResultsUntilCurrentResponse()
    {
        var queryClient = new SequencedDelayedPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");
        await queryClient.WaitForCallAsync(0);
        await cut.InvokeAsync(() => queryClient.Complete(0, SearchResultPage(PartyPickerTestData.Result(name: "Ada Lovelace"))));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Ada Lovelace"));

        cut.Find("input").Input("grace");
        await queryClient.WaitForCallAsync(1);

        cut.Find(".hx-party-picker__status").TextContent.ShouldBe("Searching parties");
        cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("Ada Lovelace");

        await cut.InvokeAsync(() => queryClient.Complete(1, SearchResultPage(PartyPickerTestData.Result(name: "Grace Hopper"))));
        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Grace Hopper");
            cut.Markup.ShouldNotContain("Ada Lovelace");
        });
    }

    [Fact]
    public void PartyPicker_MetadataUnavailable_DoesNotClaimLocalOnlyOrDegradedSearch()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe("Showing 1 of 1 matching parties");
            cut.Markup.ShouldNotContain("Local search results");
            cut.Markup.ShouldNotContain("Limited search results");
        });
    }

    [Theory]
    [InlineData(ProjectionFreshnessStatus.LocalOnly, "Local search results")]
    [InlineData(ProjectionFreshnessStatus.Degraded, "Limited search results")]
    [InlineData(ProjectionFreshnessStatus.Stale, "Limited search results")]
    [InlineData(ProjectionFreshnessStatus.Rebuilding, "Limited search results")]
    public void PartyPicker_FreshnessState_RendersBoundedNonColorOnlyStatus(
        ProjectionFreshnessStatus freshnessStatus,
        string expectedStatus)
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(
            1,
            ProjectionFreshnessMetadata.Create(freshnessStatus, "raw tenant token backend detail"),
            PartyPickerTestData.Result(name: "Ada Lovelace")));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe(expectedStatus);
            cut.FindAll("[role=\"option\"]").Count.ShouldBe(1);
            cut.Markup.ShouldNotContain("raw tenant token backend detail");
        });
    }

    [Fact]
    public void PartyPicker_InitialRender_DoesNotExposeAdvancedSearchOrTemporalControls()
    {
        RegisterClient();

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DispatchDomEvents, false));

        cut.Markup.ShouldNotContain("semantic", Case.Insensitive);
        cut.Markup.ShouldNotContain("hybrid", Case.Insensitive);
        cut.Markup.ShouldNotContain("graph", Case.Insensitive);
        cut.Markup.ShouldNotContain("temporal", Case.Insensitive);
        cut.Markup.ShouldNotContain("as of", Case.Insensitive);
        cut.FindAll("select").ShouldBeEmpty();
        cut.FindAll("[role=\"radiogroup\"]").ShouldBeEmpty();
    }

    [Fact]
    public void PartyPicker_DefaultSearch_DoesNotForwardAdvancedModeOrCaseId()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            queryClient.SearchCalls.Count.ShouldBe(1);
            queryClient.LastMode.ShouldBeNull();
            queryClient.LastCaseId.ShouldBeNull();
        });
    }

    [Fact]
    public void PartyPicker_MissingToken_ShowsAuthenticationRequiredWithoutRequest()
    {
        var queryClient = new RecordingPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe("Authentication is required");
            queryClient.SearchCalls.ShouldBeEmpty();
        });
    }

    [Fact]
    public void PartyPicker_Disabled_SetsDisabledAttributeAndDoesNotIssueSearch()
    {
        var queryClient = new RecordingPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.Disabled, true)
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("input").HasAttribute("readonly").ShouldBeFalse();
        cut.Find("input").Input("ada");

        queryClient.SearchCalls.ShouldBeEmpty();
        cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
    }

    [Fact]
    public void PartyPicker_ReadOnly_SetsReadonlyAttributeKeepsKeyboardOperableAndDoesNotIssueSearch()
    {
        var queryClient = new RecordingPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.ReadOnly, true)
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").HasAttribute("readonly").ShouldBeTrue();
        cut.Find("input").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("input").GetAttribute("aria-readonly").ShouldBe("true");
        cut.Find("input").Input("ada");

        queryClient.SearchCalls.ShouldBeEmpty();
        cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
    }

    [Fact]
    public void PartyPicker_TransientFailureRetry_ReissuesSearchAndRendersResult()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.EnqueueFailure(new HttpRequestException("temporary"));
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");
        cut.WaitForAssertion(() => cut.Find(".hx-party-picker__status").TextContent.ShouldBe("Parties search is temporarily unavailable"));

        cut.Find(".hx-party-picker__retry").GetAttribute("type").ShouldBe("button");
        cut.Find(".hx-party-picker__retry").GetAttribute("aria-label").ShouldBe("Retry search");
        cut.Find(".hx-party-picker__retry").GetAttribute("title").ShouldBe("Retry search");

        cut.Find(".hx-party-picker__retry").Click();

        cut.WaitForAssertion(() =>
        {
            queryClient.SearchCalls.Count.ShouldBe(2);
            cut.FindAll("[role=\"option\"]").Count.ShouldBe(1);
        });
    }

    [Theory]
    [InlineData(401, "Sign in again to search parties", false)]
    [InlineData(403, "You do not have access to these parties", false)]
    [InlineData(404, "No matching parties", false)]
    [InlineData(410, "No matching parties", false)]
    [InlineData(503, "Parties search is temporarily unavailable", true)]
    public void PartyPicker_SearchFailureStates_RenderBoundedStatusAndRetryOnlyWhenRetryable(
        int statusCode,
        string expectedStatus,
        bool expectsRetry)
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnSearch = new PartiesClientException(
                statusCode,
                "Problem title",
                "problem-type",
                "raw token tenant Ada Lovelace backend detail",
                "correlation-1"),
        };
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe(expectedStatus);
            cut.FindAll(".hx-party-picker__retry").Count.ShouldBe(expectsRetry ? 1 : 0);
            cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
            cut.Markup.ShouldNotContain("raw token");
            cut.Markup.ShouldNotContain("correlation-1");
        });
    }

    [Fact]
    public void PartyPicker_NotFoundOrGone_ClearsResultListAndShowsNoResults()
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnSearch = new PartiesClientException(410, "Gone", null, "raw erased party detail", "correlation-1"),
        };
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe("No matching parties");
            cut.Markup.ShouldNotContain("raw erased party detail");
        });
    }

    [Fact]
    public async Task PartyPicker_ContextChange_ClearsVisibleResultsAndSelection()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        RegisterClient(queryClient);

        PartyPickerSelection? selected = null;
        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.ContextKey, "tenant-a:user-a")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false)
            .Add(p => p.SelectedPartyChanged, value => selected = value));

        cut.Find("input").Input("ada");
        cut.WaitForAssertion(() => cut.FindAll("[role=\"option\"]").Count.ShouldBe(1));
        await cut.InvokeAsync(() => cut.Find(".hx-party-picker__result-button").Click());
        cut.WaitForAssertion(() => selected!.PartyId.ShouldBe("party-1"));

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PartyPicker.AccessToken)] = "host-token",
            [nameof(PartyPicker.ContextKey)] = "tenant-b:user-b",
            [nameof(PartyPicker.DebounceMilliseconds)] = 1,
            [nameof(PartyPicker.DispatchDomEvents)] = false,
            [nameof(PartyPicker.SelectedPartyChanged)] = EventCallback.Factory.Create<PartyPickerSelection?>(this, value => selected = value),
        })));

        cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("Ada Lovelace");
    }

    [Fact]
    public async Task PartyPicker_AuthContextKeyChange_ClearsVisibleResultsAndSelection()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token-a")
            .Add(p => p.AuthContextKey, "auth-version-a")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");
        cut.WaitForAssertion(() => cut.FindAll("[role=\"option\"]").Count.ShouldBe(1));
        await cut.InvokeAsync(() => cut.Find(".hx-party-picker__result-button").Click());

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PartyPicker.AccessToken)] = "host-token-b",
            [nameof(PartyPicker.AuthContextKey)] = "auth-version-b",
            [nameof(PartyPicker.DebounceMilliseconds)] = 1,
            [nameof(PartyPicker.DispatchDomEvents)] = false,
        })));

        cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("Ada Lovelace");
    }

    [Fact]
    public async Task PartyPicker_AccessTokenValueChange_WithSamePresence_DoesNotReissueSelectionLookup()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.EnqueueDetail(PartyDetail(id: "party-1", name: "Ada Lovelace"));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "token-version-a")
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.PageSize, 10)
            .Add(p => p.DispatchDomEvents, false));

        cut.WaitForAssertion(() =>
        {
            queryClient.GetCalls.Count.ShouldBe(1);
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("Ada Lovelace");
        });

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PartyPicker.AccessToken)] = "token-version-b",
            [nameof(PartyPicker.SelectedPartyId)] = "party-1",
            [nameof(PartyPicker.PageSize)] = 10,
            [nameof(PartyPicker.DispatchDomEvents)] = false,
        })));

        // Token VALUE changed but presence ("provided") is identical → context signature unchanged → no re-lookup
        queryClient.GetCalls.Count.ShouldBe(1);
        cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task PartyPicker_StaleSearchResponse_DoesNotRepopulateAfterContextChange()
    {
        var queryClient = new DelayedPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.ContextKey, "tenant-a:user-a")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");
        await queryClient.WaitForCallAsync();

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PartyPicker.AccessToken)] = "host-token",
            [nameof(PartyPicker.ContextKey)] = "tenant-b:user-b",
            [nameof(PartyPicker.DebounceMilliseconds)] = 1,
            [nameof(PartyPicker.DispatchDomEvents)] = false,
        })));

        queryClient.Complete(SearchResultPage(PartyPickerTestData.Result()));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
            cut.Markup.ShouldNotContain("Ada Lovelace");
        });
    }

    [Fact]
    public async Task PartyPicker_StaleSearchResponse_DoesNotRepopulateAfterSearchOptionsChange()
    {
        var queryClient = new DelayedPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.SearchMode, "lexical")
            .Add(p => p.CaseId, "case-a")
            .Add(p => p.PageSize, 10)
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");
        await queryClient.WaitForCallAsync();

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PartyPicker.AccessToken)] = "host-token",
            [nameof(PartyPicker.SearchMode)] = "lexical",
            [nameof(PartyPicker.CaseId)] = "case-b",
            [nameof(PartyPicker.PageSize)] = 5,
            [nameof(PartyPicker.DebounceMilliseconds)] = 1,
            [nameof(PartyPicker.DispatchDomEvents)] = false,
        })));

        queryClient.Complete(SearchResultPage(PartyPickerTestData.Result()));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
            cut.Markup.ShouldNotContain("Ada Lovelace");
        });
    }

    [Fact]
    public void PartyPickerEventDetail_ExcludesNamesTenantAndTokenMaterial()
    {
        var detail = PartyPickerEventDetail.FromSelection(new PartyPickerSelection
        {
            PartyId = "party-1",
            DisplayName = "Ada Lovelace",
            PartyType = PartyType.Person,
            IsActive = true,
        });

        string json = JsonSerializer.Serialize(detail);
        detail.PartyId.ShouldBe("party-1");
        json.ShouldNotContain("Ada");
        json.ShouldNotContain("tenant", Case.Insensitive);
        json.ShouldNotContain("token", Case.Insensitive);
    }

    [Fact]
    public void PartyPickerEventDetail_SerializesOnlyDurableIdAndBoundedState()
    {
        var detail = PartyPickerEventDetail.FromSelection(new PartyPickerSelection
        {
            PartyId = "party-1",
            DisplayName = "Ada Lovelace",
            PartyType = PartyType.Person,
            IsActive = true,
            SafeReason = "raw ProblemDetails token tenant searchText consent identifier contact degradedReason queryPayload",
        });

        string payload = JsonSerializer.Serialize(detail);
        string[] propertyNames = typeof(PartyPickerEventDetail)
            .GetProperties()
            .Select(static property => property.Name)
            .ToArray();

        propertyNames.ShouldBe(["PartyId", "PartyType", "Status"], ignoreOrder: true);
        payload.ShouldContain("party-1");
        payload.ShouldContain("Person");
        payload.ShouldContain("active");

        foreach (string forbidden in new[]
        {
            "Ada",
            "DisplayName",
            "contact",
            "identifier",
            "consent",
            "degraded",
            "searchText",
            "tenant",
            "token",
            "ProblemDetails",
            "queryPayload",
            "SafeReason",
        })
        {
            payload.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    [Fact]
    public async Task PartyPicker_SelectionCallbacks_EmitDurableIdOnlyAndPreviewSeparately()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result(
            id: "party-1",
            name: "Ada Lovelace")));
        RegisterClient(queryClient);

        string? durablePartyId = null;
        PartyPickerSelection? preview = null;
        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false)
            .Add(p => p.SelectedPartyIdChanged, value => durablePartyId = value)
            .Add(p => p.SelectedPartyChanged, value => preview = value));

        cut.Find("input").Input("ada");
        cut.WaitForAssertion(() => cut.FindAll("[role=\"option\"]").Count.ShouldBe(1));
        await cut.InvokeAsync(() => cut.Find(".hx-party-picker__result-button").Click());

        durablePartyId.ShouldBe("party-1");
        durablePartyId.ShouldNotBeNull();
        durablePartyId!.ShouldNotContain("Ada");
        durablePartyId.ShouldNotContain("tenant");
        durablePartyId.ShouldNotContain("token");
        preview.ShouldNotBeNull();
        preview.PartyId.ShouldBe("party-1");
        preview.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task PartyPicker_ClearSelection_EmitsNullDurableIdWithoutPreviewData()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result(
            id: "party-1",
            name: "Ada Lovelace")));
        RegisterClient(queryClient);

        List<string?> durableIds = [];
        List<PartyPickerSelection?> previews = [];
        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false)
            .Add(p => p.SelectedPartyIdChanged, durableIds.Add)
            .Add(p => p.SelectedPartyChanged, previews.Add));

        cut.Find("input").Input("ada");
        cut.WaitForAssertion(() => cut.FindAll("[role=\"option\"]").Count.ShouldBe(1));
        await cut.InvokeAsync(() => cut.Find(".hx-party-picker__result-button").Click());
        await cut.InvokeAsync(() => cut.Find(".hx-party-picker__icon-button").Click());

        durableIds.ShouldBe([null, "party-1", null]);
        durableIds.Where(static id => id is not null).ShouldAllBe(static id => !id!.Contains("Ada", StringComparison.OrdinalIgnoreCase));
        previews.Count.ShouldBe(3);
        previews[0].ShouldBeNull();
        previews[1].ShouldNotBeNull();
        previews[1]!.PartyId.ShouldBe("party-1");
        previews[1]!.DisplayName.ShouldBe("Ada Lovelace");
        previews[2].ShouldBeNull();
    }

    [Fact]
    public async Task PartyPicker_SelectionCallback_ReturnsStablePartyIdAndPreviewOnly()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result(name: "Ada Lovelace")));
        RegisterClient(queryClient);

        PartyPickerSelection? selected = null;
        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false)
            .Add(p => p.SelectedPartyChanged, value => selected = value));

        cut.Find("input").Input("ada");
        cut.WaitForAssertion(() => cut.FindAll("[role=\"option\"]").Count.ShouldBe(1));
        await cut.InvokeAsync(() => cut.Find(".hx-party-picker__result-button").Click());

        selected.ShouldNotBeNull();
        selected.PartyId.ShouldBe("party-1");
        selected.DisplayName.ShouldBe("Ada Lovelace");
        selected.ToString().ShouldNotContain("tenant");
        selected.ToString().ShouldNotContain("token");
    }

    [Fact]
    public void PartyPicker_PreselectedPartyIdWithoutAuth_RendersBoundedStateWithoutLookup()
    {
        var queryClient = new RecordingPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.DispatchDomEvents, false));

        cut.WaitForAssertion(() =>
        {
            queryClient.GetCalls.ShouldBeEmpty();
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-1");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("Authentication is required to view the selected party");
        });
    }

    [Fact]
    public void PartyPicker_PreselectedPartyId_ResolvesDisplayThroughTypedClient()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.EnqueueDetail(PartyDetail(id: "party-1", name: "Ada Lovelace"));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.DispatchDomEvents, false));

        cut.WaitForAssertion(() =>
        {
            queryClient.GetCalls.ShouldBe(["party-1"]);
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("Ada Lovelace");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("Active");
        });
    }

    [Fact]
    public async Task PartyPicker_PreselectedPartyId_ShowsLoadingStateUntilDisplayResolves()
    {
        var queryClient = new DelayedSelectedPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.DispatchDomEvents, false));

        await queryClient.WaitForGetCallAsync(0);

        cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-1");
        cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("Loading selected party");

        await cut.InvokeAsync(() => queryClient.CompleteGet(0, PartyDetail(id: "party-1", name: "Ada Lovelace")));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("Ada Lovelace");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("Active");
        });
    }

    [Fact]
    public void PartyPicker_PreselectedErasedParty_KeepsDurableIdAndRendersBoundedStatus()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.EnqueueDetail(PartyDetail(id: "party-erased", name: string.Empty, erased: true));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.SelectedPartyId, "party-erased")
            .Add(p => p.DispatchDomEvents, false));

        cut.WaitForAssertion(() =>
        {
            queryClient.GetCalls.ShouldBe(["party-erased"]);
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-erased");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("Erased");
        });
    }

    [Theory]
    [InlineData(401, "Sign in again to view the selected party")]
    [InlineData(403, "Selected party is not available in this authorized context")]
    [InlineData(404, "Selected party was not found")]
    [InlineData(410, "Selected party is no longer available")]
    [InlineData(503, "Selected party details are temporarily unavailable")]
    public void PartyPicker_PreselectedPartyUnavailable_RendersBoundedStateWithoutReplacingDurableId(
        int statusCode,
        string expectedLabel)
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnGet = new PartiesClientException(
                statusCode,
                "Problem title",
                "problem-type",
                "raw token Ada Lovelace backend selected detail",
                "correlation-1"),
        };
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.SelectedPartyId, "party-raw")
            .Add(p => p.DispatchDomEvents, false));

        cut.WaitForAssertion(() =>
        {
            queryClient.GetCalls.ShouldBe(["party-raw"]);
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-raw");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe(expectedLabel);
            cut.Markup.ShouldNotContain("raw token");
            cut.Markup.ShouldNotContain("Ada Lovelace");
            cut.Markup.ShouldNotContain("correlation-1");
        });
    }

    [Fact]
    public void PartyPicker_PreselectedTransientFailureRetry_UsesCurrentSelectedDisplayContext()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.EnqueueGetFailure(new HttpRequestException("raw token Ada Lovelace transport detail"));
        queryClient.EnqueueDetail(PartyDetail(id: "party-1", name: "Ada Lovelace"));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.DispatchDomEvents, false));

        cut.WaitForAssertion(() =>
        {
            queryClient.GetCalls.ShouldBe(["party-1"]);
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-1");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("Selected party details are temporarily unavailable");
            cut.Find(".hx-party-picker__retry").TextContent.ShouldBe("Retry selected party");
            cut.Markup.ShouldNotContain("raw token");
        });

        cut.Find(".hx-party-picker__retry").Click();

        cut.WaitForAssertion(() =>
        {
            queryClient.GetCalls.ShouldBe(["party-1", "party-1"]);
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("Ada Lovelace");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("Active");
        });
    }

    [Fact]
    public async Task PartyPicker_StaleSelectedDisplayResponse_DoesNotRepopulateAfterContextChange()
    {
        var queryClient = new DelayedSelectedPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.ContextKey, "tenant-a:user-a")
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.DispatchDomEvents, false));

        await queryClient.WaitForGetCallAsync(0);

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PartyPicker.AccessToken)] = "host-token",
            [nameof(PartyPicker.ContextKey)] = "tenant-b:user-b",
            [nameof(PartyPicker.SelectedPartyId)] = "party-2",
            [nameof(PartyPicker.DispatchDomEvents)] = false,
        })));

        queryClient.CompleteGet(0, PartyDetail(id: "party-1", name: "Ada Lovelace"));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-2");
            cut.Markup.ShouldNotContain("Ada Lovelace");
        });
    }

    [Fact]
    public void PartyPicker_SelectedPartyIdLookupFailure_DoesNotRenderMisleadingActiveBadge()
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnGet = new PartiesClientException(404, "Not Found", null, "raw detail", "correlation-1"),
        };
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.DispatchDomEvents, false));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-1");
            cut.Find(".hx-party-picker__badge").TextContent.ShouldBe("Selected party was not found");
            cut.Markup.ShouldNotContain("Active");
        });
    }

    [Fact]
    public void PartyPicker_Enabled_ExposesInteractiveControlsAndRoutesQueryThroughTypedClient()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("input").HasAttribute("readonly").ShouldBeFalse();
        cut.Find("input").GetAttribute("aria-controls").ShouldNotBeNullOrWhiteSpace();

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            queryClient.SearchCalls.Count.ShouldBe(1);
            queryClient.SearchCalls[0].Query.ShouldBe("ada");
            queryClient.SearchCalls[0].Page.ShouldBe(1);
            queryClient.SearchCalls[0].PageSize.ShouldBe(10);
            cut.FindAll("[role=\"option\"]").Count.ShouldBe(1);
        });
    }

    [Fact]
    public void PartyPicker_CompactLayout_RendersSingleBoundedRootAndWrapsLongNames()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result(
            name: "Maximilian Alexander Bartholomew Featherstonehaugh-Worthington III")));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        // A single bounded embeddable root - not a multi-pane admin surface.
        cut.FindAll("section.hx-party-picker").Count.ShouldBe(1);
        cut.FindAll(".hx-party-picker__label").Count.ShouldBe(1);
        cut.FindAll(".hx-party-picker__input-row").Count.ShouldBe(1);

        cut.Find("input").Input("max");

        cut.WaitForAssertion(() =>
        {
            // Long names render inside the wrap-enabled name slot so the compact layout cannot overflow.
            cut.Find(".hx-party-picker__result-name").TextContent
                .ShouldContain("Featherstonehaugh-Worthington");
            cut.FindAll("section.hx-party-picker").Count.ShouldBe(1);
        });
    }

    [Fact]
    public void PartyPicker_CompactLayout_RendersBoundedBadgeForLongLocalizedStatus()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result(name: "Northwind Trading")));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.Labels, new PartyPickerLabels
            {
                Active = "Selection status requiring several localized words",
            })
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("northwind");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__badge").TextContent
                .ShouldBe("Selection status requiring several localized words");
        });
    }

    [Fact]
    public void PartyPicker_LayoutCss_DeclaresBoundedCompactContract()
    {
        string cssPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Hexalith.Parties.Picker",
            "Components",
            "PartyPicker.razor.css");
        string css = File.ReadAllText(cssPath);

        css.ShouldContain("grid-template-columns: minmax(0, 1fr) 2.25rem");
        css.ShouldContain("max-width: min(100%, 32rem)");
        css.ShouldContain("max-width");
        css.ShouldContain("overflow-wrap: anywhere");
        css.ShouldContain(":focus-visible");
        css.ShouldContain("@media (forced-colors: active)");
        css.ShouldContain("forced-color-adjust: none");
        css.ShouldContain("@media (prefers-reduced-motion: reduce)");
        css.ShouldContain("max-width: min(100%, 8rem)");
        css.ShouldContain("white-space: normal");
        css.ShouldNotContain("white-space: nowrap");
        css.ShouldNotContain("content:");
    }

    [Fact]
    public void PartyPicker_BackendContractUnavailable_ShowsBoundedUnavailableStateWithRetryAndNoLeak()
    {
        var queryClient = new RecordingPartiesQueryClient
        {
            ThrowOnSearch = new InvalidOperationException("raw 0xCAFEF00D backend stack Lovelace detail"),
        };
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("northwind");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe("Parties search is unavailable");
            cut.FindAll(".hx-party-picker__retry").Count.ShouldBe(1);
            cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
            // The raw backend exception detail must never reach the rendered shell.
            cut.Markup.ShouldNotContain("0xCAFEF00D");
            cut.Markup.ShouldNotContain("Lovelace");
            cut.Markup.ShouldNotContain("backend stack");
        });
    }

    [Fact]
    public void PartyPicker_DisabledWithSelectedPartyId_KeepsSelectionDisplayStableAndAccessible()
    {
        var queryClient = new RecordingPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.Disabled, true)
            .Add(p => p.DispatchDomEvents, false));

        // Selection display stays present and carries an accessible region name while disabled.
        cut.Find(".hx-party-picker__selected").GetAttribute("aria-label").ShouldBe("Selected party");
        cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-1");
        cut.Find("input").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".hx-party-picker__icon-button").HasAttribute("disabled").ShouldBeTrue();
        queryClient.SearchCalls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void PartyPicker_DisabledOrReadOnlyInputEvent_DoesNotClearSelectionOrNotifyHost(
        bool disabled,
        bool readOnly)
    {
        var queryClient = new RecordingPartiesQueryClient();
        RegisterClient(queryClient);
        List<PartyPickerSelection?> previewCallbacks = [];
        List<string?> durableCallbacks = [];

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters =>
        {
            parameters.Add(p => p.AccessToken, "host-token");
            parameters.Add(p => p.SelectedPartyId, "party-1");
            parameters.Add(p => p.Disabled, disabled);
            parameters.Add(p => p.ReadOnly, readOnly);
            parameters.Add(p => p.DispatchDomEvents, false);
            parameters.Add(p => p.SelectedPartyChanged, previewCallbacks.Add);
            parameters.Add(p => p.SelectedPartyIdChanged, durableCallbacks.Add);
        });

        cut.Find("input").Input("ada");

        cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-1");
        previewCallbacks.ShouldBeEmpty();
        durableCallbacks.ShouldBeEmpty();
        queryClient.SearchCalls.ShouldBeEmpty();
    }

    [Fact]
    public void PartyPicker_ClearButton_UsesAccessibleDecorativeIconPattern()
    {
        RegisterClient();

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DispatchDomEvents, false));

        var clearButton = cut.Find(".hx-party-picker__icon-button");

        // Accessible name comes from the localized label, not the decorative glyph.
        clearButton.GetAttribute("type").ShouldBe("button");
        clearButton.GetAttribute("aria-label").ShouldBe("Clear selected party");
        clearButton.GetAttribute("title").ShouldBe("Clear selected party");

        // The visible glyph is a decorative close icon hidden from assistive technology.
        var glyph = cut.Find(".hx-party-picker__icon-button .hx-party-picker__icon");
        glyph.GetAttribute("aria-hidden").ShouldBe("true");
        clearButton.TextContent.Trim().ShouldBe("×");
    }

    private void RegisterClient(RecordingPartiesQueryClient? queryClient = null)
    {
        queryClient ??= new RecordingPartiesQueryClient();
        Services.AddScoped(_ => new PartyPickerApiClient(queryClient));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Parties.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Hexalith.Parties.slnx from test output directory.");
    }

    private static PagedResult<PartySearchResult> SearchResultPage(params PartySearchResult[] results)
        => SearchResultPage(results.Length, results);

    private static PagedResult<PartySearchResult> SearchResultPage(int totalCount, params PartySearchResult[] results)
        => SearchResultPage(totalCount, null, results);

    private static PagedResult<PartySearchResult> SearchResultPage(
        int totalCount,
        ProjectionFreshnessMetadata? freshness,
        params PartySearchResult[] results)
        => new()
        {
            Items = results,
            Page = 1,
            PageSize = Math.Max(1, results.Length),
            TotalCount = totalCount,
            TotalPages = results.Length == 0 ? 0 : 1,
            Freshness = freshness,
        };

    private static PartyDetail PartyDetail(
        string id = "party-1",
        string name = "Ada Lovelace",
        PartyType type = PartyType.Person,
        bool active = true,
        bool erased = false)
        => new()
        {
            Id = id,
            Type = type,
            IsActive = active,
            DisplayName = name,
            SortName = name,
            CreatedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
            IsErased = erased,
        };

    private sealed class DelayedPartiesQueryClient : RecordingPartiesQueryClient
    {
        private readonly TaskCompletionSource<PagedResult<PartySearchResult>> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _called = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForCallAsync()
            => _called.Task;

        public void Complete(PagedResult<PartySearchResult> response)
            => _response.TrySetResult(response);

        public override Task<PagedResult<PartySearchResult>> SearchPartiesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken ct,
            string? mode = null,
            string? caseId = null,
            Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
        {
            SearchCalls.Add(new SearchCall(query, page, pageSize));
            LastMode = mode;
            LastCaseId = caseId;
            LastRequestCustomizer = requestCustomizer;
            _called.TrySetResult();
            return _response.Task.WaitAsync(ct);
        }
    }

    private sealed class SequencedDelayedPartiesQueryClient : RecordingPartiesQueryClient
    {
        private readonly List<TaskCompletionSource<PagedResult<PartySearchResult>>> _responses = [];
        private readonly List<TaskCompletionSource> _calls = [];

        public Task WaitForCallAsync(int index)
        {
            EnsureSlot(index);
            return _calls[index].Task;
        }

        public void Complete(int index, PagedResult<PartySearchResult> response)
        {
            EnsureSlot(index);
            _responses[index].TrySetResult(response);
        }

        public override Task<PagedResult<PartySearchResult>> SearchPartiesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken ct,
            string? mode = null,
            string? caseId = null,
            Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
        {
            int index = SearchCalls.Count;
            EnsureSlot(index);
            SearchCalls.Add(new SearchCall(query, page, pageSize));
            LastMode = mode;
            LastCaseId = caseId;
            LastRequestCustomizer = requestCustomizer;
            _calls[index].TrySetResult();
            return _responses[index].Task.WaitAsync(ct);
        }

        private void EnsureSlot(int index)
        {
            while (_responses.Count <= index)
            {
                _responses.Add(new TaskCompletionSource<PagedResult<PartySearchResult>>(TaskCreationOptions.RunContinuationsAsynchronously));
                _calls.Add(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            }
        }
    }

    private sealed class DelayedSelectedPartiesQueryClient : RecordingPartiesQueryClient
    {
        private readonly List<TaskCompletionSource<PartyDetail>> _responses = [];
        private readonly List<TaskCompletionSource> _calls = [];

        public Task WaitForGetCallAsync(int index)
        {
            EnsureSlot(index);
            return _calls[index].Task;
        }

        public void CompleteGet(int index, PartyDetail detail)
        {
            EnsureSlot(index);
            _responses[index].TrySetResult(detail);
        }

        public override Task<PartyDetail> GetPartyAsync(
            string partyId,
            CancellationToken ct,
            Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
        {
            int index = GetCalls.Count;
            EnsureSlot(index);
            GetCalls.Add(partyId);
            LastRequestCustomizer = requestCustomizer;
            _calls[index].TrySetResult();
            return _responses[index].Task.WaitAsync(ct);
        }

        private void EnsureSlot(int index)
        {
            while (_responses.Count <= index)
            {
                _responses.Add(new TaskCompletionSource<PartyDetail>(TaskCreationOptions.RunContinuationsAsynchronously));
                _calls.Add(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            }
        }
    }

    [Fact]
    public async Task PartyPicker_StaleSelectedDisplayResponse_DoesNotRepopulateAfterOnlySelectedPartyIdChange()
    {
        var queryClient = new DelayedSelectedPartiesQueryClient();
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.ContextKey, "tenant-a:user-a")
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.DispatchDomEvents, false));

        await queryClient.WaitForGetCallAsync(0);

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PartyPicker.AccessToken)] = "host-token",
            [nameof(PartyPicker.ContextKey)] = "tenant-a:user-a",
            [nameof(PartyPicker.SelectedPartyId)] = "party-2",
            [nameof(PartyPicker.DispatchDomEvents)] = false,
        })));

        queryClient.CompleteGet(0, PartyDetail(id: "party-1", name: "Ada Lovelace"));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-2");
            cut.Markup.ShouldNotContain("Ada Lovelace");
        });
    }
}
