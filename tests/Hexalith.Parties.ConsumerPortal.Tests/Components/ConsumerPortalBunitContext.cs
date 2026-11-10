using Bunit;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

/// <summary>Renders consumer pages through an interactive Server renderer unless a test selects another renderer.</summary>
public abstract class ConsumerPortalBunitContext : BunitContext
{
    /// <summary>Gets or sets the renderer applied when a page renders; the pages gate input on its interactivity.</summary>
    protected RendererInfo PageRenderer { get; set; } = new("Server", isInteractive: true);

    /// <inheritdoc/>
    public override IRenderedComponent<TComponent> Render<TComponent>(Action<ComponentParameterCollectionBuilder<TComponent>>? parameterBuilder = null)
    {
        // Selecting the renderer resolves it, which freezes service registration, so apply it only when rendering.
        SetRendererInfo(PageRenderer);
        return base.Render(parameterBuilder);
    }
}
