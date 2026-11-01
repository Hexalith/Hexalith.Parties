using Hexalith.EventStore.Admin.UI;
using Hexalith.EventStore.ServiceDefaults;

namespace Hexalith.Parties.EventStoreAdminUI.TestHost;

/// <summary>Preserves native SDK UI composition while this explicitly client-only host declares no Dapr subscription receiver.</summary>
public static class PrivateAdminUiComposition
{
    /// <summary>Uses the unchanged native SDK service registration and sidecar presence guard.</summary>
    public static WebApplicationBuilder AddNativeAdminUiServices(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddServiceDefaults();
        builder.RequireDaprSidecar();
        return builder.AddAdminUI();
    }

    /// <summary>Uses the unchanged native pipeline for the protocol comparison control.</summary>
    public static WebApplication UseNativeAdminUi(WebApplication app) => app.UseAdminUI();

    /// <summary>Maps the actual SDK Razor application and UI middleware without creating a subscription-discovery endpoint.</summary>
    /// <param name="app">The private Development UI application with actual SDK Admin UI services.</param>
    /// <returns>The configured application.</returns>
    public static WebApplication UsePrivateAuxiliaryAdminUI(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _ = app.UseExceptionHandler();
        if (!app.Environment.IsDevelopment()) { _ = app.UseHsts(); }
        // Dapr's real HTTP 404 means no subscriptions. Preserve native redirect exclusion
        // so the unmatched discovery request cannot become an HTTPS redirect.
        _ = app.UseWhen(
            static context => !(HttpMethods.IsGet(context.Request.Method)
                && string.Equals(context.Request.Path.Value, "/dapr/subscribe", StringComparison.OrdinalIgnoreCase)),
            static branch => { _ = branch.UseHttpsRedirection(); });
        _ = app.UseAntiforgery();
        _ = app.MapStaticAssets();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        _ = app.MapRazorComponents<Hexalith.EventStore.Admin.UI.Components.App>()
            .AddInteractiveServerRenderMode();
        return app;
    }
}
