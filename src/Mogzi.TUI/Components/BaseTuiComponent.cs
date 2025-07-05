using System.Runtime.CompilerServices;

namespace Mogzi.TUI.Components;

/// <summary>
/// Base implementation of ITuiComponent that provides dirty tracking functionality.
/// All components should inherit from this class to get automatic dirty tracking.
/// </summary>
public abstract class BaseTuiComponent : ITuiComponent
{
    private bool _isVisible = true;

    /// <summary>
    /// Gets the unique name of the component.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets or sets whether the component is visible and should be rendered.
    /// Setting this property marks the component as dirty.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Gets whether the component needs to be re-rendered due to state changes.
    /// </summary>
    public bool IsDirty { get; private set; } = true;

    /// <summary>
    /// Marks the component as dirty, requiring re-rendering.
    /// </summary>
    public void MarkDirty()
    {
        IsDirty = true;
    }

    /// <summary>
    /// Marks the component as clean after rendering.
    /// </summary>
    public void MarkClean()
    {
        IsDirty = false;
    }

    /// <summary>
    /// Renders the component and returns the renderable content.
    /// Automatically marks the component as clean after rendering.
    /// </summary>
    /// <param name="context">The rendering context containing shared state and services</param>
    /// <returns>The rendered content</returns>
    public IRenderable Render(IRenderContext context)
    {
        var content = RenderContent(context);
        MarkClean();
        return content;
    }

    /// <summary>
    /// Abstract method that derived classes must implement to provide their rendering logic.
    /// </summary>
    /// <param name="context">The rendering context containing shared state and services</param>
    /// <returns>The rendered content</returns>
    protected abstract IRenderable RenderContent(IRenderContext context);

    /// <summary>
    /// Handles input events for the component.
    /// </summary>
    /// <param name="context">The rendering context containing shared state and services</param>
    /// <param name="inputEvent">The input event to handle</param>
    /// <returns>True if the input was handled, false otherwise</returns>
    public virtual Task<bool> HandleInputAsync(IRenderContext context, object inputEvent)
    {
        // Default implementation doesn't handle any input
        return Task.FromResult(false);
    }

    /// <summary>
    /// Initializes the component with the given context.
    /// </summary>
    /// <param name="context">The rendering context containing shared state and services</param>
    public virtual Task InitializeAsync(IRenderContext context)
    {
        context.Logger.LogDebug("{ComponentName} initialized", Name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes of the component and cleans up resources.
    /// </summary>
    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper method for derived classes to mark dirty when state changes.
    /// </summary>
    /// <typeparam name="T">The type of the property</typeparam>
    /// <param name="field">Reference to the backing field</param>
    /// <param name="value">The new value</param>
    /// <param name="propertyName">The name of the property (automatically filled by compiler)</param>
    /// <returns>True if the value changed, false otherwise</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        MarkDirty();
        return true;
    }
}
