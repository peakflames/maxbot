namespace Mogzi.TUI.Components;

/// <summary>
/// Manages the lifecycle and coordination of TUI components.
/// Handles component registration, layout composition, and input distribution.
/// </summary>
public class TuiComponentManager : ITuiComponentManager
{
    private readonly Dictionary<string, ITuiComponent> _components = [];
    private readonly ILogger<TuiComponentManager> _logger;
    private IRenderable? _lastLayoutRender;
    private string? _lastLayoutCacheKey;
    private string? _lastInputText;
    private int _lastCursorPosition;

    public IReadOnlyDictionary<string, ITuiComponent> Components => _components.AsReadOnly();
    public ITuiLayout? CurrentLayout { get; set; }

    public TuiComponentManager(ILogger<TuiComponentManager> logger, IRenderCache renderCache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // renderCache parameter is required by DI but not currently used in this implementation
        ArgumentNullException.ThrowIfNull(renderCache);
    }

    public void RegisterComponent(ITuiComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_components.ContainsKey(component.Name))
        {
            _logger.LogWarning("Component {ComponentName} is already registered, replacing existing component", component.Name);
        }

        _components[component.Name] = component;
        _logger.LogDebug("Registered component: {ComponentName}", component.Name);
    }

    public bool UnregisterComponent(string componentName)
    {
        ArgumentException.ThrowIfNullOrEmpty(componentName);

        if (_components.Remove(componentName))
        {
            _logger.LogDebug("Unregistered component: {ComponentName}", componentName);
            return true;
        }

        _logger.LogWarning("Attempted to unregister non-existent component: {ComponentName}", componentName);
        return false;
    }

    public ITuiComponent? GetComponent(string componentName)
    {
        ArgumentException.ThrowIfNullOrEmpty(componentName);
        return _components.GetValueOrDefault(componentName);
    }

    public T? GetComponent<T>(string componentName) where T : class, ITuiComponent
    {
        return GetComponent(componentName) as T;
    }

    public IRenderable RenderLayout(IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (CurrentLayout == null)
        {
            _logger.LogWarning("No layout set, returning empty content");
            return new Text("No layout configured");
        }

        if (!CurrentLayout.ValidateComponents(_components))
        {
            var requiredComponents = CurrentLayout.GetRequiredComponents();
            var missingComponents = requiredComponents.Where(name => !_components.ContainsKey(name));
            _logger.LogError("Layout validation failed. Missing components: {MissingComponents}", string.Join(", ", missingComponents));
            return new Text($"Layout error: Missing components: {string.Join(", ", missingComponents)}");
        }

        try
        {
            // Check if any visible components are dirty
            var visibleComponents = _components.Values.Where(c => c.IsVisible).ToList();
            var anyDirty = visibleComponents.Any(c => c.IsDirty);

            // Create cache key based on current state and visible components
            var cacheKey = $"layout_{CurrentLayout.Name}_{context.CurrentState}_{string.Join(",", visibleComponents.Select(c => c.Name))}";

            // If nothing is dirty and we have cached content, return it
            if (!anyDirty && _lastLayoutCacheKey == cacheKey && _lastLayoutRender != null)
            {
                _logger.LogDebug("Returning cached layout render for key: {CacheKey}", cacheKey);
                return _lastLayoutRender;
            }

            // Render the layout
            var rendered = CurrentLayout.Compose(_components, context);

            // Cache the result
            _lastLayoutRender = rendered;
            _lastLayoutCacheKey = cacheKey;

            _logger.LogDebug("Rendered and cached layout for key: {CacheKey}, dirty components: {DirtyComponents}",
                cacheKey, string.Join(",", visibleComponents.Where(c => c.IsDirty).Select(c => c.Name)));
            return rendered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering layout: {LayoutName}", CurrentLayout.Name);
            return new Text($"Layout rendering error: {ex.Message}");
        }
    }

    public async Task<bool> BroadcastInputAsync(object inputEvent, IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(inputEvent);
        ArgumentNullException.ThrowIfNull(context);

        var handled = false;

        foreach (var component in _components.Values.Where(c => c.IsVisible))
        {
            try
            {
                if (await component.HandleInputAsync(context, inputEvent))
                {
                    handled = true;
                    _logger.LogDebug("Input event handled by component: {ComponentName}", component.Name);
                    break; // Stop at first component that handles the input
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling input in component: {ComponentName}", component.Name);
            }
        }

        return handled;
    }

    public async Task InitializeComponentsAsync(IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogDebug("Initializing {ComponentCount} components", _components.Count);

        foreach (var component in _components.Values)
        {
            try
            {
                await component.InitializeAsync(context);
                _logger.LogDebug("Initialized component: {ComponentName}", component.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing component: {ComponentName}", component.Name);
            }
        }

        _logger.LogDebug("Component initialization complete");
    }

    public async Task DisposeComponentsAsync()
    {
        _logger.LogDebug("Disposing {ComponentCount} components", _components.Count);

        foreach (var component in _components.Values)
        {
            try
            {
                await component.DisposeAsync();
                _logger.LogDebug("Disposed component: {ComponentName}", component.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing component: {ComponentName}", component.Name);
            }
        }

        _components.Clear();
        _logger.LogDebug("Component disposal complete");
    }

    public void SetComponentVisibility(string componentName, bool isVisible)
    {
        ArgumentException.ThrowIfNullOrEmpty(componentName);

        if (_components.TryGetValue(componentName, out var component))
        {
            // Only update and log if visibility actually changes
            if (component.IsVisible != isVisible)
            {
                component.IsVisible = isVisible;
                component.MarkDirty(); // Mark component as dirty when visibility changes
                _logger.LogDebug("Set component {ComponentName} visibility to {IsVisible}", componentName, isVisible);
            }
        }
        else
        {
            _logger.LogWarning("Attempted to set visibility for non-existent component: {ComponentName}", componentName);
        }
    }

    public void UpdateComponentVisibility(ChatState currentState, IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Check for input context changes and mark InputPanel as dirty if needed
        CheckInputContextChanges(context);

        // Update component visibility based on current state
#pragma warning disable IDE0010 // Add missing cases
        switch (currentState)
        {
            case ChatState.Input:
                SetComponentVisibility("InputPanel", true);
                SetComponentVisibility("FooterPanel", true);
                SetComponentVisibility("ProgressPanel", false);

                // Show autocomplete/user selection panels based on input context
                var inputContext = context.TuiContext.InputContext;
                SetComponentVisibility("AutocompletePanel", inputContext.State == InputState.Autocomplete && inputContext.ShowSuggestions);
                SetComponentVisibility("UserSelectionPanel", inputContext.State == InputState.UserSelection);

                // Show welcome panel if no chat history
                var chatHistory = context.TuiContext.HistoryManager.GetCurrentChatHistory();
                SetComponentVisibility("WelcomePanel", !chatHistory.Any());
                break;

            case ChatState.Thinking:
            case ChatState.ToolExecution:
                SetComponentVisibility("InputPanel", false);
                SetComponentVisibility("AutocompletePanel", false);
                SetComponentVisibility("UserSelectionPanel", false);
                SetComponentVisibility("WelcomePanel", false);
                SetComponentVisibility("ProgressPanel", true);
                SetComponentVisibility("FooterPanel", true);
                
                // CRITICAL: Mark animated components as dirty to ensure they re-render with new animation frames
                MarkAnimatedComponentsDirty(currentState);
                break;

            default:
                _logger.LogWarning("Unknown chat state: {CurrentState}", currentState);
                break;
        }
#pragma warning restore IDE0010 // Add missing cases

        _logger.LogDebug("Updated component visibility for state: {CurrentState}", currentState);
    }

    private void CheckInputContextChanges(IRenderContext context)
    {
        var inputContext = context.TuiContext.InputContext;
        var currentInputText = inputContext.CurrentInput;
        var currentCursorPosition = inputContext.CursorPosition;

        // Check if input text or cursor position has changed
        if (_lastInputText != currentInputText || _lastCursorPosition != currentCursorPosition)
        {
            // Only mark InputPanel as dirty if it would actually result in different rendered content
            if (_components.TryGetValue("InputPanel", out var inputPanel))
            {
                // Check if the InputPanel is visible and the change would affect rendering
                if (inputPanel.IsVisible)
                {
                    inputPanel.MarkDirty();
                    _logger.LogDebug("DIRTY TRACKING: Marked InputPanel as dirty due to input context change: '{LastInput}' -> '{CurrentInput}', cursor: {LastCursor} -> {CurrentCursor}",
                        _lastInputText ?? "", currentInputText, _lastCursorPosition, currentCursorPosition);
                }
            }
            else
            {
                _logger.LogWarning("DIRTY TRACKING: InputPanel component not found in components dictionary");
            }

            // Update tracking variables
            _lastInputText = currentInputText;
            _lastCursorPosition = currentCursorPosition;
        }
    }

    private void MarkAnimatedComponentsDirty(ChatState currentState)
    {
        // Mark components that contain animations as dirty so they re-render with new animation frames
        // This is critical for spinner animations to work properly

#pragma warning disable IDE0010 // Add missing cases
        switch (currentState)
        {
            case ChatState.Thinking:
            case ChatState.ToolExecution:
                // ProgressPanel contains spinner animations that need to update every frame
                if (_components.TryGetValue("ProgressPanel", out var progressPanel) && progressPanel.IsVisible)
                {
                    progressPanel.MarkDirty();
                    _logger.LogDebug("ANIMATION: Marked ProgressPanel as dirty for animation update in state {CurrentState}", currentState);
                }

                // FooterPanel may contain time-based information that needs updates
                if (_components.TryGetValue("FooterPanel", out var footerPanel) && footerPanel.IsVisible)
                {
                    footerPanel.MarkDirty();
                    _logger.LogDebug("ANIMATION: Marked FooterPanel as dirty for potential time updates in state {CurrentState}", currentState);
                }
                break;
            default:
                break;
        }
#pragma warning restore IDE0010 // Add missing cases
    }
}
