namespace Mogzi.TUI.Components;

/// <summary>
/// Base interface for all TUI components.
/// Defines the contract for component rendering, input handling, and lifecycle management.
/// </summary>
public interface ITuiComponent
{
    /// <summary>
    /// Gets the unique name of the component.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets or sets whether the component is visible and should be rendered.
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// Gets whether the component needs to be re-rendered due to state changes.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Marks the component as dirty, requiring re-rendering.
    /// </summary>
    void MarkDirty();

    /// <summary>
    /// Marks the component as clean after rendering.
    /// </summary>
    void MarkClean();

    /// <summary>
    /// Renders the component and returns the renderable content.
    /// </summary>
    /// <param name="context">The rendering context containing shared state and services</param>
    /// <returns>The rendered content</returns>
    IRenderable Render(IRenderContext context);

    /// <summary>
    /// Handles input events for the component.
    /// </summary>
    /// <param name="context">The rendering context containing shared state and services</param>
    /// <param name="inputEvent">The input event to handle</param>
    /// <returns>True if the input was handled, false otherwise</returns>
    Task<bool> HandleInputAsync(IRenderContext context, object inputEvent);

    /// <summary>
    /// Initializes the component with the given context.
    /// </summary>
    /// <param name="context">The rendering context containing shared state and services</param>
    Task InitializeAsync(IRenderContext context);

    /// <summary>
    /// Disposes of the component and cleans up resources.
    /// </summary>
    Task DisposeAsync();
}
