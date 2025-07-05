using Mogzi.TUI.Infrastructure;

namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Simulates keyboard input for integration testing.
/// Provides methods to simulate real keyboard events through the TestKeyboardHandler.
/// </summary>
public class KeyboardSimulator
{
    private readonly TestKeyboardHandler _keyboardHandler;
    private readonly ILogger<KeyboardSimulator> _logger;

    public KeyboardSimulator(TestKeyboardHandler keyboardHandler, ILogger<KeyboardSimulator> logger)
    {
        _keyboardHandler = keyboardHandler ?? throw new ArgumentNullException(nameof(keyboardHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Simulates typing a string of characters.
    /// </summary>
    public async Task TypeStringAsync(string text, TimeSpan? delayBetweenChars = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var delay = delayBetweenChars ?? TimeSpan.FromMilliseconds(10);
        
        _logger.LogDebug("Simulating typing: '{Text}'", text);

        foreach (var character in text)
        {
            await SimulateCharacterAsync(character);
            
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }
        }
    }

    /// <summary>
    /// Simulates typing a single character.
    /// </summary>
    public async Task SimulateCharacterAsync(char character)
    {
        _logger.LogDebug("Simulating character: '{Character}'", character);

        _keyboardHandler.SimulateCharacter(character);
        await Task.Delay(10); // Small delay to allow processing
    }

    /// <summary>
    /// Simulates pressing a specific key.
    /// </summary>
    public async Task SimulateKeyPressAsync(ConsoleKey key, ConsoleModifiers modifiers = ConsoleModifiers.None)
    {
        _logger.LogDebug("Simulating key press: {Key} + {Modifiers}", key, modifiers);

        var keyChar = GetKeyChar(key, modifiers);
        _keyboardHandler.SimulateKeyPress(key, modifiers, keyChar);
        await Task.Delay(10); // Small delay to allow processing
    }

    /// <summary>
    /// Simulates pressing Enter key.
    /// </summary>
    public async Task SimulateEnterAsync()
    {
        await SimulateKeyPressAsync(ConsoleKey.Enter);
    }

    /// <summary>
    /// Simulates pressing Backspace key.
    /// </summary>
    public async Task SimulateBackspaceAsync()
    {
        await SimulateKeyPressAsync(ConsoleKey.Backspace);
    }

    /// <summary>
    /// Simulates pressing Tab key.
    /// </summary>
    public async Task SimulateTabAsync()
    {
        await SimulateKeyPressAsync(ConsoleKey.Tab);
    }

    /// <summary>
    /// Simulates pressing Escape key.
    /// </summary>
    public async Task SimulateEscapeAsync()
    {
        await SimulateKeyPressAsync(ConsoleKey.Escape);
    }

    /// <summary>
    /// Simulates pressing arrow keys.
    /// </summary>
    public async Task SimulateArrowKeyAsync(ConsoleKey arrowKey)
    {
        if (arrowKey != ConsoleKey.LeftArrow && 
            arrowKey != ConsoleKey.RightArrow && 
            arrowKey != ConsoleKey.UpArrow && 
            arrowKey != ConsoleKey.DownArrow)
        {
            throw new ArgumentException("Key must be an arrow key", nameof(arrowKey));
        }

        await SimulateKeyPressAsync(arrowKey);
    }

    /// <summary>
    /// Simulates Ctrl+C (interrupt signal).
    /// </summary>
    public async Task SimulateCtrlCAsync()
    {
        await SimulateKeyPressAsync(ConsoleKey.C, ConsoleModifiers.Control);
    }

    /// <summary>
    /// Simulates Ctrl+L (clear screen).
    /// </summary>
    public async Task SimulateCtrlLAsync()
    {
        await SimulateKeyPressAsync(ConsoleKey.L, ConsoleModifiers.Control);
    }

    /// <summary>
    /// Simulates Ctrl+P (previous command in history).
    /// </summary>
    public async Task SimulateCtrlPAsync()
    {
        await SimulateKeyPressAsync(ConsoleKey.P, ConsoleModifiers.Control);
    }

    /// <summary>
    /// Simulates Ctrl+N (next command in history).
    /// </summary>
    public async Task SimulateCtrlNAsync()
    {
        await SimulateKeyPressAsync(ConsoleKey.N, ConsoleModifiers.Control);
    }

    /// <summary>
    /// Simulates a complete command input (typing + Enter).
    /// </summary>
    public async Task SimulateCommandAsync(string command, TimeSpan? typingDelay = null)
    {
        _logger.LogDebug("Simulating command: '{Command}'", command);

        await TypeStringAsync(command, typingDelay);
        await Task.Delay(50); // Small delay before Enter
        await SimulateEnterAsync();
    }

    /// <summary>
    /// Simulates clearing current input with multiple backspaces.
    /// </summary>
    public async Task SimulateClearInputAsync(int characterCount, TimeSpan? delayBetweenKeys = null)
    {
        var delay = delayBetweenKeys ?? TimeSpan.FromMilliseconds(10);
        
        _logger.LogDebug("Simulating clear input: {CharacterCount} backspaces", characterCount);

        for (int i = 0; i < characterCount; i++)
        {
            await SimulateBackspaceAsync();
            
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }
        }
    }

    /// <summary>
    /// Gets the character representation for a key press.
    /// </summary>
    private static char GetKeyChar(ConsoleKey key, ConsoleModifiers modifiers)
    {
        return key switch
        {
            ConsoleKey.Enter => '\r',
            ConsoleKey.Tab => '\t',
            ConsoleKey.Spacebar => ' ',
            >= ConsoleKey.A and <= ConsoleKey.Z => (modifiers & ConsoleModifiers.Shift) != 0 
                ? (char)('A' + (key - ConsoleKey.A))
                : (char)('a' + (key - ConsoleKey.A)),
            >= ConsoleKey.D0 and <= ConsoleKey.D9 => (char)('0' + (key - ConsoleKey.D0)),
            _ => '\0'
        };
    }
}

/// <summary>
/// Extension methods for KeyboardSimulator to provide fluent API.
/// </summary>
public static class KeyboardSimulatorExtensions
{
    /// <summary>
    /// Simulates a sequence of keyboard actions with delays.
    /// </summary>
    public static async Task SimulateSequenceAsync(this KeyboardSimulator simulator, 
        params (Func<KeyboardSimulator, Task> action, TimeSpan delay)[] sequence)
    {
        foreach (var (action, delay) in sequence)
        {
            await action(simulator);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }
        }
    }

    /// <summary>
    /// Simulates typing with realistic human-like delays.
    /// </summary>
    public static async Task TypeLikeHumanAsync(this KeyboardSimulator simulator, string text)
    {
        var random = new Random();
        
        foreach (var character in text)
        {
            await simulator.SimulateCharacterAsync(character);
            
            // Random delay between 50-150ms to simulate human typing
            var delay = TimeSpan.FromMilliseconds(random.Next(50, 150));
            await Task.Delay(delay);
        }
    }
}
