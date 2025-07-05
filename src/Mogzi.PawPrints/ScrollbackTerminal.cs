namespace Mogzi.PawPrints;

public class ScrollbackTerminal(IAnsiConsole console) : IScrollbackTerminal
{
    private readonly IAnsiConsole _console = console;
    private readonly Lock _lock = new();
    private int _dynamicContentLineCount = 0;
    private int _updatableContentLineCount = 0;
    private bool _isShutdown = false;
    private string? _lastUpdatableContentHash = null;

    public void Initialize()
    {
        _console.Clear();
        _console.Cursor.SetPosition(0, 0);
        _console.Cursor.Hide();
    }

    public void WriteStatic(IRenderable content, bool isUpdatable = false)
    {
        if (_isShutdown)
        {
            return;
        }

        lock (_lock)
        {
            ClearDynamicContent();

            // Check for duplicate updatable content to prevent race condition duplicates
            if (isUpdatable)
            {
                var writer = new StringWriter();
                var measuringConsole = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer), ColorSystem = ColorSystemSupport.NoColors });
                measuringConsole.Write(content);
                var output = writer.ToString();
                var currentContentHash = output.GetHashCode().ToString();

                if (currentContentHash == _lastUpdatableContentHash)
                {
                    // Skip duplicate updatable content
                    return;
                }

                _lastUpdatableContentHash = currentContentHash;
                ClearUpdatableContent();

                var lineCount = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Length;
                _updatableContentLineCount = lineCount;
            }
            else
            {
                // Only clear updatable content if this is an updatable write
                // This prevents tool execution outputs from overwriting assistant messages
                var writer = new StringWriter();
                var measuringConsole = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer), ColorSystem = ColorSystemSupport.NoColors });
                measuringConsole.Write(content);
                var output = writer.ToString();
                var lineCount = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Length;
            }

            _console.Write(content);
            _console.WriteLine();
        }
    }

    public async Task StartDynamicDisplayAsync(Func<IRenderable> dynamicContentProvider, CancellationToken cancellationToken)
    {
        // Optimized frame rate - only update when content changes or at most 5 FPS (200ms) for responsiveness
        const int frameDelayMs = 200;
        string? lastContentHash = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_isShutdown)
            {
                break;
            }

            var dynamicContent = dynamicContentProvider();
            
            // Calculate a simple hash of the content to detect changes
            var currentContentHash = GetContentHash(dynamicContent);
            
            // Only update if content has changed
            if (currentContentHash != lastContentHash)
            {
                UpdateDynamic(dynamicContent);
                lastContentHash = currentContentHash;
            }

            try
            {
                await Task.Delay(frameDelayMs, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private string GetContentHash(IRenderable content)
    {
        try
        {
            var writer = new StringWriter();
            var measuringConsole = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer), ColorSystem = ColorSystemSupport.NoColors });
            measuringConsole.Write(content);
            var output = writer.ToString();
            return output.GetHashCode().ToString();
        }
        catch
        {
            // If we can't hash the content, assume it changed
            return DateTime.UtcNow.Ticks.ToString();
        }
    }

    public void Shutdown()
    {
        if (_isShutdown)
        {
            return;
        }

        _isShutdown = true;

        lock (_lock)
        {
            ClearDynamicContent();
        }
        _console.Cursor.Show();
    }

    private void UpdateDynamic(IRenderable content)
    {
        if (_isShutdown)
        {
            return;
        }

        lock (_lock)
        {
            ClearDynamicContent();

            var writer = new StringWriter();
            var measuringConsole = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer), ColorSystem = ColorSystemSupport.NoColors });
            measuringConsole.Write(content);
            var output = writer.ToString();
            _dynamicContentLineCount = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Length;

            _console.Write(content);
        }
    }

    private void ClearDynamicContent()
    {
        if (_dynamicContentLineCount > 0)
        {
            try
            {
                // Ensure we don't try to move up more lines than available
                var linesToMoveUp = Math.Min(_dynamicContentLineCount - 1, _console.Profile.Height - 1);
                if (linesToMoveUp > 0)
                {
                    _console.Cursor.MoveUp(linesToMoveUp);
                }
                _console.Write("\x1b[0J");
            }
            catch (Exception)
            {
                // In test environments or when console operations fail, 
                // gracefully handle the error without throwing
                if (!_isShutdown)
                {
                    // For test environments, just clear the line count without throwing
                    _dynamicContentLineCount = 0;
                    return;
                }
            }
        }
        _dynamicContentLineCount = 0;
    }

    private void ClearUpdatableContent()
    {
        if (_updatableContentLineCount > 0)
        {
            try
            {
                // Ensure we don't try to move up more lines than available
                var linesToMoveUp = Math.Min(_updatableContentLineCount, _console.Profile.Height - 1);
                if (linesToMoveUp > 0)
                {
                    _console.Cursor.MoveUp(linesToMoveUp);
                }
                _console.Write("\x1b[0J");
            }
            catch (Exception)
            {
                // In test environments or when console operations fail, 
                // gracefully handle the error without throwing
                if (!_isShutdown)
                {
                    // For test environments, just clear the line count without throwing
                    _updatableContentLineCount = 0;
                    return;
                }
            }
        }
        _updatableContentLineCount = 0;
    }
}
