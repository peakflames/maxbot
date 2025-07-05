
namespace Mogzi.TUI.State;

/// <summary>
/// Concrete implementation of ITuiStateManager that manages the state machine for the TUI application.
/// </summary>
public class TuiStateManager(ILogger<TuiStateManager> logger) : ITuiStateManager
{
    private readonly ILogger<TuiStateManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Dictionary<ChatState, Func<ITuiState>> _stateFactories = [];
    private readonly Lock _stateLock = new();

    private ITuiContext? _context;
    private ITuiState? _currentState;
    private ChatState _currentStateType = ChatState.None;

    public ITuiState? CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
    }

    public ChatState CurrentStateType
    {
        get
        {
            lock (_stateLock)
            {
                return _currentStateType;
            }
        }
    }

    public Task InitializeAsync(ITuiContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger.LogDebug("TuiStateManager initialized with context");

        // Start with Input state
        return TransitionToStateAsync(ChatState.Input);
    }

    public async Task TransitionToStateAsync(ChatState newStateType)
    {
        if (_context == null)
        {
            throw new InvalidOperationException("TuiStateManager must be initialized before transitioning states");
        }

        ITuiState? previousState;
        ITuiState? newState;

        lock (_stateLock)
        {
            if (_currentStateType == newStateType)
            {
                _logger.LogDebug("Already in state {StateType}, skipping transition", newStateType);
                return;
            }

            previousState = _currentState;

            // Create new state instance
            if (!_stateFactories.TryGetValue(newStateType, out var stateFactory))
            {
                throw new InvalidOperationException($"No state factory registered for state type: {newStateType}");
            }

            newState = stateFactory();
            _currentState = newState;
            _currentStateType = newStateType;
        }

        try
        {
            _logger.LogDebug("RACE_CONDITION_DEBUG: Starting transition from {PreviousState} to {NewState} at {Timestamp}",
                previousState?.Name ?? "none", newState.Name, DateTime.UtcNow.ToString("HH:mm:ss.fff"));

            // Call exit on previous state
            if (previousState != null)
            {
                _logger.LogDebug("RACE_CONDITION_DEBUG: Calling OnExitAsync for {PreviousState}", previousState.Name);
                await previousState.OnExitAsync(_context, newState);
                _logger.LogDebug("RACE_CONDITION_DEBUG: OnExitAsync completed for {PreviousState}", previousState.Name);
            }

            // Call enter on new state
            _logger.LogDebug("RACE_CONDITION_DEBUG: Calling OnEnterAsync for {NewState}", newState.Name);
            await newState.OnEnterAsync(_context, previousState);
            _logger.LogDebug("RACE_CONDITION_DEBUG: OnEnterAsync completed for {NewState}", newState.Name);

            _logger.LogDebug("Successfully transitioned to {NewState}", newState.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during state transition from {PreviousState} to {NewState}",
                previousState?.Name ?? "none", newState.Name);
            throw;
        }
    }

    public IRenderable RenderDynamicContent()
    {
        if (_context == null || _currentStateType == ChatState.None || _currentState == null)
        {
            return new Text("State manager not initialized");
        }

        try
        {
            return _currentState.RenderDynamicContent(_context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering dynamic content for state {StateName}", _currentState.Name);
            return new Text($"Error rendering {_currentState.Name} state: {ex.Message}");
        }
    }

    public async Task HandleKeyPressAsync(KeyPressEventArgs e)
    {
        if (_context == null || _currentStateType == ChatState.None || _currentState == null)
        {
            return;
        }

        try
        {
            await _currentState.HandleKeyPressAsync(_context, e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling key press in state {StateName}: {Key}",
                _currentState.Name, e.Key);
        }
    }

    public async Task HandleCharacterTypedAsync(CharacterTypedEventArgs e)
    {
        if (_context == null || _currentStateType == ChatState.None || _currentState == null)
        {
            return;
        }

        try
        {
            await _currentState.HandleCharacterTypedAsync(_context, e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling character typed in state {StateName}: {Character}",
                _currentState.Name, e.Character);
        }
    }

    public void RegisterState(ChatState stateType, Func<ITuiState> stateFactory)
    {
        ArgumentNullException.ThrowIfNull(stateFactory);

        lock (_stateLock)
        {
            _stateFactories[stateType] = stateFactory;
        }

        _logger.LogDebug("Registered state factory for {StateType}", stateType);
    }

    public async Task ShutdownAsync()
    {
        ITuiState? currentState;

        lock (_stateLock)
        {
            currentState = _currentState;
            _currentState = null;
            _currentStateType = ChatState.None;
        }

        if (currentState != null && _context != null)
        {
            try
            {
                await currentState.OnExitAsync(_context, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during state shutdown for {StateName}", currentState.Name);
            }
        }

        _logger.LogDebug("TuiStateManager shutdown complete");
    }
}
