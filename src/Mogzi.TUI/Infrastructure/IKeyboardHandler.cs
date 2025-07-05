namespace Mogzi.TUI.Infrastructure;

/// <summary>
/// Interface for keyboard input handling with event-driven architecture.
/// </summary>
public interface IKeyboardHandler : IDisposable
{
    /// <summary>
    /// Event raised when a key is pressed.
    /// </summary>
    event EventHandler<KeyPressEventArgs>? KeyPressed;

    /// <summary>
    /// Event raised when a key combination is pressed.
    /// </summary>
    event EventHandler<KeyCombinationEventArgs>? KeyCombinationPressed;

    /// <summary>
    /// Event raised when a character is typed (excludes control keys).
    /// </summary>
    event EventHandler<CharacterTypedEventArgs>? CharacterTyped;

    /// <summary>
    /// Gets whether the keyboard handler is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current keyboard input statistics.
    /// </summary>
    KeyboardStatistics Statistics { get; }

    /// <summary>
    /// Starts the keyboard input handling.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the keyboard input handling.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Registers a key binding for a specific key combination.
    /// </summary>
    void RegisterKeyBinding(ConsoleKey key, ConsoleModifiers modifiers, Action<KeyPressEventArgs> handler);

    /// <summary>
    /// Registers a key binding for a single key without modifiers.
    /// </summary>
    void RegisterKeyBinding(ConsoleKey key, Action<KeyPressEventArgs> handler);

    /// <summary>
    /// Unregisters a key binding.
    /// </summary>
    void UnregisterKeyBinding(ConsoleKey key, ConsoleModifiers modifiers = ConsoleModifiers.None);

    /// <summary>
    /// Gets the current keyboard input statistics.
    /// </summary>
    KeyboardStatistics GetStatistics();
}
