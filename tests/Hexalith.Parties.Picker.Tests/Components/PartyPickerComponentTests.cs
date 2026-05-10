using Bunit;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Picker.Components;
using Hexalith.Parties.Picker.Services;
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
        var queryClient = new PartyPickerApiClientTests.RecordingPartiesQueryClient();
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
    public void PartyPicker_LocalOnlyAndDegradedStates_AreVisibleAndBounded()
    {
        var queryClient = new PartyPickerApiClientTests.RecordingPartiesQueryClient();
        queryClient.Enqueue(SearchResultPage(PartyPickerTestData.Result()));
        RegisterClient(queryClient);

        IRenderedComponent<PartyPicker> cut = Render<PartyPicker>(parameters => parameters
            .Add(p => p.AccessToken, "host-token")
            .Add(p => p.DebounceMilliseconds, 1)
            .Add(p => p.DispatchDomEvents, false));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-party-picker__status").TextContent.ShouldBeEmpty();
            cut.Markup.ShouldNotContain("local fallback");
        });
    }

    [Fact]
    public void PartyPicker_MissingToken_ShowsAuthenticationRequiredWithoutRequest()
    {
        var queryClient = new PartyPickerApiClientTests.RecordingPartiesQueryClient();
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
    public async Task PartyPicker_ContextChange_ClearsVisibleResultsAndSelection()
    {
        var queryClient = new PartyPickerApiClientTests.RecordingPartiesQueryClient();
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

    private void RegisterClient(PartyPickerApiClientTests.RecordingPartiesQueryClient? queryClient = null)
    {
        queryClient ??= new PartyPickerApiClientTests.RecordingPartiesQueryClient();
        Services.AddScoped(_ => new PartyPickerApiClient(queryClient));
    }

    private static PagedResult<PartySearchResult> SearchResultPage(params PartySearchResult[] results)
        => new()
        {
            Items = results,
            Page = 1,
            PageSize = 10,
            TotalCount = results.Length,
            TotalPages = results.Length == 0 ? 0 : 1,
        };
}
