using Mogzi.TUI.Infrastructure;

namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Test-friendly keyboard handler that doesn't rely on console access.
/// Allows programmatic simulation of keyboard events for integration testing.
/// </summary>
public sealed class TestKeyboardHandler : IKeyboardHandler
{
    private readonly ILogger<TestKeyboardHandler> _logger;
    private readonly Dictionary<KeyBinding, Action<KeyPressEventArgs>> _keyBindings = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Queue<ConsoleKeyInfo> _keyQueue = new();
    private readonly object _queueLock = new();
    private Task? _inputTask;
    private bool _isDisposed = false;

    /// <summary>
    /// Event raised when a key is pressed.
    /// </summary>
    public event EventHandler<KeyPressEventArgs>? KeyPressed;

    /// <summary>
    /// Event raised when a key combination is pressed.
    /// </summary>
    public event EventHandler<KeyCombinationEventArgs>? KeyCombinationPressed;

    /// <summary>
    /// Event raised when a character is typed (excludes control keys).
    /// </summary>
    public event EventHandler<CharacterTypedEventArgs>? CharacterTyped;

    /// <summary>
    /// Gets whether the keyboard handler is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the current keyboard input statistics.
    /// </summary>
    public KeyboardStatistics Statistics { get; private set; } = new();

    /// <summary>
    /// Initializes a new instance of TestKeyboardHandler.
    /// </summary>
    public TestKeyboardHandler(ILogger<TestKeyboardHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("TestKeyboardHandler initialized");
    }

    /// <summary>
    /// Starts the keyboard input handling.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (IsRunning)
        {
            throw new InvalidOperationException("Keyboard handler is already running");
        }

        _logger.LogDebug("Starting test keyboard input handling");

        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token);

        IsRunning = true;
        _inputTask = HandleKeyboardInputAsync(combinedCts.Token);

        try
        {
            await _inputTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test keyboard input handling");
            throw;
        }
        finally
        {
            IsRunning = false;
            _logger.LogDebug("Test keyboard input handling stopped");
        }
    }

    /// <summary>
    /// Stops the keyboard input handling.
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        _logger.LogDebug("Stopping test keyboard input handling");
        _cancellationTokenSource.Cancel();

        if (_inputTask != null)
        {
            try
            {
                await _inputTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }
    }

    /// <summary>
    /// Registers a key binding for a specific key combination.
    /// </summary>
    public void RegisterKeyBinding(ConsoleKey key, ConsoleModifiers modifiers, Action<KeyPressEventArgs> handler)
    {
        if (_isDisposed)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(handler);

        var binding = new KeyBinding(key, modifiers);
        _keyBindings[binding] = handler;

        _logger.LogDebug("Registered key binding: {Key} + {Modifiers}", key, modifiers);
    }

    /// <summary>
    /// Registers a key binding for a single key without modifiers.
    /// </summary>
    public void RegisterKeyBinding(ConsoleKey key, Action<KeyPressEventArgs> handler)
    {
        RegisterKeyBinding(key, ConsoleModifiers.None, handler);
    }

    /// <summary>
    /// Unregisters a key binding.
    /// </summary>
    public void UnregisterKeyBinding(ConsoleKey key, ConsoleModifiers modifiers = ConsoleModifiers.None)
    {
        if (_isDisposed)
        {
            return;
        }

        var binding = new KeyBinding(key, modifiers);
        if (_keyBindings.Remove(binding))
        {
            _logger.LogDebug("Unregistered key binding: {Key} + {Modifiers}", key, modifiers);
        }
    }

    /// <summary>
    /// Simulates a key press by adding it to the internal queue.
    /// </summary>
    public void SimulateKeyPress(ConsoleKeyInfo keyInfo)
    {
        if (_isDisposed)
        {
            return;
        }

        lock (_queueLock)
        {
            _keyQueue.Enqueue(keyInfo);
        }

        _logger.LogDebug("Simulated key press: {Key} + {Modifiers}", keyInfo.Key, keyInfo.Modifiers);
    }

    /// <summary>
    /// Simulates a key press with the specified key and modifiers.
    /// </summary>
    public void SimulateKeyPress(ConsoleKey key, ConsoleModifiers modifiers = ConsoleModifiers.None, char keyChar = '\0')
    {
        var keyInfo = new ConsoleKeyInfo(keyChar, key,
            (modifiers & ConsoleModifiers.Shift) != 0,
            (modifiers & ConsoleModifiers.Alt) != 0,
            (modifiers & ConsoleModifiers.Control) != 0);

        SimulateKeyPress(keyInfo);
    }

    /// <summary>
    /// Simulates typing a character.
    /// </summary>
    public void SimulateCharacter(char character)
    {
        var key = CharacterToConsoleKey(character);
        var modifiers = char.IsUpper(character) ? ConsoleModifiers.Shift : ConsoleModifiers.None;
        
        SimulateKeyPress(key, modifiers, character);
    }

    /// <summary>
    /// Simulates typing a string of characters.
    /// </summary>
    public void SimulateString(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var character in text)
        {
            SimulateCharacter(character);
        }
    }

    /// <summary>
    /// Gets the current keyboard input statistics.
    /// </summary>
    public KeyboardStatistics GetStatistics()
    {
        return Statistics with
        {
            IsRunning = IsRunning,
            RegisteredBindingsCount = _keyBindings.Count
        };
    }

    /// <summary>
    /// Handles keyboard input from the simulated queue.
    /// </summary>
    private async Task HandleKeyboardInputAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting test keyboard input loop");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ConsoleKeyInfo? keyInfo = null;

                lock (_queueLock)
                {
                    if (_keyQueue.Count > 0)
                    {
                        keyInfo = _keyQueue.Dequeue();
                    }
                }

                if (keyInfo.HasValue)
                {
                    await ProcessKeyInputAsync(keyInfo.Value);

                    // Update statistics
                    Statistics = Statistics with
                    {
                        TotalKeysProcessed = Statistics.TotalKeysProcessed + 1,
                        LastKeyPressTime = DateTime.UtcNow
                    };
                }
                else
                {
                    // Small delay to prevent busy waiting
                    await Task.Delay(10, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test keyboard input handling");
            throw;
        }

        _logger.LogDebug("Test keyboard input loop stopped");
    }

    /// <summary>
    /// Processes a single key input event.
    /// </summary>
    private async Task ProcessKeyInputAsync(ConsoleKeyInfo keyInfo)
    {
        try
        {
            var keyPressArgs = new KeyPressEventArgs(keyInfo);

            // Check for registered key bindings first
            var binding = new KeyBinding(keyInfo.Key, keyInfo.Modifiers);
            if (_keyBindings.TryGetValue(binding, out var handler))
            {
                handler(keyPressArgs);
                if (keyPressArgs.Handled)
                {
                    return;
                }
            }

            // Raise key combination event for complex key combinations
            if (keyInfo.Modifiers != ConsoleModifiers.None)
            {
                var combinationArgs = new KeyCombinationEventArgs(keyInfo.Key, keyInfo.Modifiers, keyInfo.KeyChar);
                KeyCombinationPressed?.Invoke(this, combinationArgs);
                if (combinationArgs.Handled)
                {
                    return;
                }
            }

            // Raise character typed event for printable characters
            if (!char.IsControl(keyInfo.KeyChar) && keyInfo.KeyChar != '\0')
            {
                var charArgs = new CharacterTypedEventArgs(keyInfo.KeyChar);
                CharacterTyped?.Invoke(this, charArgs);
                if (charArgs.Handled)
                {
                    return;
                }
            }

            // Raise general key pressed event
            KeyPressed?.Invoke(this, keyPressArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing key input: {Key}", keyInfo.Key);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Converts a character to its corresponding ConsoleKey.
    /// </summary>
    private static ConsoleKey CharacterToConsoleKey(char character)
    {
        return character switch
        {
            >= 'a' and <= 'z' => (ConsoleKey)(character - 'a' + (int)ConsoleKey.A),
            >= 'A' and <= 'Z' => (ConsoleKey)(character - 'A' + (int)ConsoleKey.A),
            >= '0' and <= '9' => (ConsoleKey)(character - '0' + (int)ConsoleKey.D0),
            ' ' => ConsoleKey.Spacebar,
            '\t' => ConsoleKey.Tab,
            '\r' or '\n' => ConsoleKey.Enter,
            _ => ConsoleKey.A // Default fallback
        };
    }

    /// <summary>
    /// Disposes the keyboard handler and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Stop the input handling
        _cancellationTokenSource.Cancel();

        // Wait for the input task to complete (with timeout)
        if (_inputTask != null && !_inputTask.IsCompleted)
        {
            try
            {
                _ = _inputTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        _cancellationTokenSource.Dispose();
        _keyBindings.Clear();

        lock (_queueLock)
        {
            _keyQueue.Clear();
        }

        // Clear event subscriptions
        KeyPressed = null;
        KeyCombinationPressed = null;
        CharacterTyped = null;

        _logger.LogDebug("TestKeyboardHandler disposed");

        GC.SuppressFinalize(this);
    }
}
