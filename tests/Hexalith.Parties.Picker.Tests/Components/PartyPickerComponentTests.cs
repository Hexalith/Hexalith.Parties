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
        cut.Markup.ShouldContain("role=\"status\"");
        cut.Markup.ShouldNotContain("listbox");
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
            cut.Find(".hx-party-picker__status").TextContent.ShouldBe("Showing 2 matching parties");
            cut.Markup.ShouldNotContain("-1");
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

        cut.Find(".hx-party-picker__retry").Click();

        cut.WaitForAssertion(() =>
        {
            queryClient.SearchCalls.Count.ShouldBe(2);
            cut.FindAll("[role=\"option\"]").Count.ShouldBe(1);
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
    public async Task PartyPicker_TokenChange_ClearsVisibleResultsAndSelection()
    {
        var queryClient = new RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token-a")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");
        cut.WaitForAssertion(() => cut.FindAll("[role=\"option\"]").Count.ShouldBe(1));
        await cut.InvokeAsync(() => cut.Find(".hx-party-picker__result-button").Click());

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PartyPicker.AccessToken)] = "host-token-b",
            [nameof(PartyPicker.DebounceMilliseconds)] = 1,
            [nameof(PartyPicker.DispatchDomEvents)] = false,
        })));

        cut.FindAll("[role=\"option\"]").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("Ada Lovelace");
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

        detail.PartyId.ShouldBe("party-1");
        detail.ToString().ShouldNotContain("Ada");
        detail.ToString().ShouldNotContain("tenant");
        detail.ToString().ShouldNotContain("token");
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
    public void PartyPicker_StubSelectionFromSelectedPartyId_DoesNotRenderMisleadingStatusBadge()
    {
        RegisterClient();

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.SelectedPartyId, "party-1")
            .Add(p => p.DispatchDomEvents, false));

        cut.FindAll(".hx-party-picker__badge").Count.ShouldBe(0);
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
        css.ShouldContain("max-width: min(100%, 8rem)");
        css.ShouldContain("white-space: normal");
        css.ShouldNotContain("white-space: nowrap");
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
        List<PartyPickerSelection?> callbacks = [];

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters =>
        {
            parameters.Add(p => p.AccessToken, "host-token");
            parameters.Add(p => p.SelectedPartyId, "party-1");
            parameters.Add(p => p.Disabled, disabled);
            parameters.Add(p => p.ReadOnly, readOnly);
            parameters.Add(p => p.DispatchDomEvents, false);
            parameters.Add(p => p.SelectedPartyChanged, callbacks.Add);
        });

        cut.Find("input").Input("ada");

        cut.Find(".hx-party-picker__selected-name").TextContent.ShouldBe("party-1");
        callbacks.ShouldBeEmpty();
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
        => new()
        {
            Items = results,
            Page = 1,
            PageSize = Math.Max(1, results.Length),
            TotalCount = totalCount,
            TotalPages = results.Length == 0 ? 0 : 1,
        };

    private sealed class DelayedPartiesQueryClient : RecordingPartiesQueryClient
    {
        private readonly TaskCompletionSource<PagedResult<PartySearchResult>> _response = new();
        private readonly TaskCompletionSource _called = new();

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
}
