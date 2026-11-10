using Bunit;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Parties.AdminPortal.Tests.Components;

/// <summary>Renders admin pages through an interactive Server renderer unless a test selects another renderer.</summary>
public abstract class AdminPortalBunitContext : BunitContext
{
    /// <summary>Gets or sets the renderer applied when a page renders; the create/edit page gates input on its interactivity.</summary>
    protected RendererInfo PageRenderer { get; set; } = new("Server", isInteractive: true);

    /// <inheritdoc/>
    public override IRenderedComponent<TComponent> Render<TComponent>(Action<ComponentParameterCollectionBuilder<TComponent>>? parameterBuilder = null)
    {
        // Selecting the renderer resolves it, which freezes service registration, so apply it only when rendering.
        SetRendererInfo(PageRenderer);
        return base.Render(parameterBuilder);
    }
}
