namespace Mogzi.TUI.App;

public sealed class FlexColumnTuiApp : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FlexColumnTuiApp> _logger;
    private readonly ITuiStateManager _stateManager;
    private readonly ITuiContext _tuiContext;
    private readonly ITuiComponentManager _componentManager;
    private readonly ITuiMediator _mediator;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IKeyboardHandler _keyboardHandler;
    private readonly IScrollbackTerminal _scrollbackTerminal;
    private readonly HistoryManager _historyManager;
    private bool _isDisposed = false;

    public bool IsRunning { get; private set; } = false;

    public event Action? Started;
    public event Action? Stopped;
    public event Action<Exception>? UnhandledError;

    public FlexColumnTuiApp(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = serviceProvider.GetRequiredService<ILogger<FlexColumnTuiApp>>();
        _stateManager = serviceProvider.GetRequiredService<ITuiStateManager>();
        _tuiContext = serviceProvider.GetRequiredService<ITuiContext>();
        _componentManager = serviceProvider.GetRequiredService<ITuiComponentManager>();
        _mediator = serviceProvider.GetRequiredService<ITuiMediator>();
        _scrollbackTerminal = serviceProvider.GetRequiredService<IScrollbackTerminal>();
        _historyManager = serviceProvider.GetRequiredService<HistoryManager>();

        _cancellationTokenSource = new CancellationTokenSource();

        _keyboardHandler = serviceProvider.GetRequiredService<IKeyboardHandler>();

        _keyboardHandler.KeyPressed += OnKeyPressed;
        _keyboardHandler.KeyCombinationPressed += OnKeyCombinationPressed;
        _keyboardHandler.CharacterTyped += OnCharacterTyped;

        RegisterKeyBindings();

        _logger.LogDebug("FlexColumnTuiApp initialized with component architecture");
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (IsRunning)
        {
            throw new InvalidOperationException("Application is already running");
        }

        try
        {
            _logger.LogInformation("Starting FlexColumn TUI application");

            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

            Initialize(args);

            IsRunning = true;
            Started?.Invoke();

            var exitCode = await RunMainLoopAsync(combinedCts.Token);

            _logger.LogInformation("FlexColumn TUI application completed with exit code {ExitCode}", exitCode);
            return exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("FlexColumn TUI application cancelled by user");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in FlexColumn TUI application");
            UnhandledError?.Invoke(ex);
            return 1;
        }
        finally
        {
            IsRunning = false;
            Stopped?.Invoke();
            await ShutdownAsync();
        }
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private async void Initialize(string[] args)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        _logger.LogDebug("Initializing FlexColumn TUI application");
        _scrollbackTerminal.Initialize();

        // Initialize state manager with state factories
        RegisterStateFactories();
        await _stateManager.InitializeAsync(_tuiContext);

        LoadCommandHistory();
        RenderInitialContent();
        _logger.LogDebug("FlexColumn TUI application initialized");
    }

    private void RegisterStateFactories()
    {
        _stateManager.RegisterState(ChatState.Input, _serviceProvider.GetRequiredService<InputTuiState>);
        _stateManager.RegisterState(ChatState.Thinking, _serviceProvider.GetRequiredService<ThinkingTuiState>);
        _stateManager.RegisterState(ChatState.ToolExecution, _serviceProvider.GetRequiredService<ToolExecutionTuiState>);
    }

    private void RenderInitialContent()
    {
        var chatHistory = _historyManager.GetCurrentChatHistory();
        if (chatHistory.Any())
        {
            // Only render chat history as static content
            RenderHistory();
        }
        else
        {
            // Render welcome message once in static content when no chat history exists
            _scrollbackTerminal.WriteStatic(CreateWelcomeMessage());
        }
    }

    private void RenderHistory()
    {
        var chatHistory = _historyManager.GetCurrentChatHistory();
        ChatRole? previousRole = null;
        foreach (var message in chatHistory)
        {
            if (previousRole != message.Role)
            {
                _scrollbackTerminal.WriteStatic(new Markup(""));
            }

            _scrollbackTerminal.WriteStatic(RenderMessage(message));

            if (previousRole != message.Role)
            {
                _scrollbackTerminal.WriteStatic(new Markup(""));
            }

            previousRole = message.Role;
        }
    }

    private async Task<int> RunMainLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting FlexColumn Live loop");

        try
        {
            _logger.LogDebug("About to start keyboard handler - IsRunning: {IsRunning}", _keyboardHandler.IsRunning);

            var keyboardTask = _keyboardHandler.StartAsync(cancellationToken);
            _logger.LogDebug("Keyboard handler StartAsync called, starting dynamic display");

            var dynamicDisplayTask = _scrollbackTerminal.StartDynamicDisplayAsync(RenderDynamicContent, cancellationToken);
            _logger.LogDebug("Both tasks started, waiting for completion");

            await Task.WhenAll(keyboardTask, dynamicDisplayTask);
            _logger.LogDebug("Both tasks completed");

            return 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("RunMainLoopAsync cancelled");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FlexColumn Live loop");
            return 1;
        }
    }

    private IRenderable RenderDynamicContent()
    {
        try
        {
            // Create render context for components
            var renderingUtilities = _serviceProvider.GetRequiredService<IRenderingUtilities>();
            var themeInfo = _serviceProvider.GetRequiredService<IThemeInfo>();
            var renderContext = new RenderContext(
                _tuiContext,
                _stateManager.CurrentStateType,
                _logger,
                _serviceProvider,
                renderingUtilities,
                themeInfo
            );

            // Only update component visibility if the state has actually changed
            // This prevents unnecessary re-renders when content is the same
            _componentManager.UpdateComponentVisibility(_stateManager.CurrentStateType, renderContext);

            // Render the layout using the component manager
            return _componentManager.RenderLayout(renderContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering dynamic content through component manager");
            return new Text($"Rendering error: {ex.Message}");
        }
    }


    private IRenderable CreateWelcomeMessage()
    {
        var contentItems = new List<IRenderable>
        {
            // Shadow effect style with multi-color letters - "Mogzi" with each letter different color
            new Markup("[bold blue]███╗   ███╗[/] [bold cyan] ██████╗ [/] [bold green] ██████╗ [/] [bold yellow]███████╗[/] [bold magenta]██╗[/]"),
            new Markup("[bold blue]████╗ ████║[/] [bold cyan]██╔═══██╗[/] [bold green]██╔════╝ [/] [bold yellow]╚══███╔╝[/] [bold magenta]██║[/]"),
            new Markup("[bold blue]██╔████╔██║[/] [bold cyan]██║   ██║[/] [bold green]██║  ███╗[/] [bold yellow]  ███╔╝ [/] [bold magenta]██║[/]"),
            new Markup("[bold blue]██║╚██╔╝██║[/] [bold cyan]██║   ██║[/] [bold green]██║   ██║[/] [bold yellow] ███╔╝  [/] [bold magenta]██║[/]"),
            new Markup("[bold blue]██║ ╚═╝ ██║[/] [bold cyan]╚██████╔╝[/] [bold green]╚██████╔╝[/] [bold yellow]███████╗[/] [bold magenta]██║[/]"),
            new Markup("[bold blue]╚═╝     ╚═╝[/] [bold cyan] ╚═════╝ [/] [bold green] ╚═════╝ [/] [bold yellow]╚══════╝[/] [bold magenta]╚═╝[/]"),
            new Text(""),
            new Markup("[bold cyan]◢◤◢◤◢◤ Now connected to your Multi-model Autonomous Assistant ◢◤◢◤◢◤[/]"),
            new Text(""),
            new Markup("[dim]Your AI-powered development assistant[/]"),
            new Text(""),
            new Markup("[grey69]Tips for getting started:[/]"),
            new Markup("[grey69]1. Ask questions, edit files, or run commands[/]"),
            new Markup("[grey69]2. Be specific for the best results[/]"),
            new Markup("[grey69]3. Use [/][magenta]/help[/][dim] for more information[/]"),
            new Text("")
        };
        return new Rows(contentItems);
    }



    private IRenderable RenderMessage(ChatMessage message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            return new Text(string.Empty);
        }

        var messageType = GetMessageType(message);
        var prefix = messageType switch
        {
            MessageType.User => "[dim]>[/] ",
            MessageType.Assistant => "✦ ",
            _ => ""
        };
        var color = messageType switch
        {
            MessageType.User => "dim",
            MessageType.Assistant => "skyblue1",
            _ => "white"
        };

        // Strip system environment context from user messages for display
        var displayText = message.Role == ChatRole.User
            ? Mogzi.Utils.MessageUtils.StripSystemEnvironment(message.Text)
            : message.Text;

        return new Markup($"[{color}]{prefix}{displayText}[/]");
    }

    private MessageType GetMessageType(ChatMessage message)
    {
        if (message.Role == ChatRole.User)
        {
            return MessageType.User;
        }
        else if (message.Role == ChatRole.Assistant)
        {
            return MessageType.Assistant;
        }
        else
        {
            return MessageType.System;
        }
    }


    private void RegisterKeyBindings()
    {
        _keyboardHandler.RegisterKeyBinding(ConsoleKey.C, ConsoleModifiers.Control, args =>
        {
            _logger.LogInformation("Ctrl+C detected, requesting shutdown");
            _cancellationTokenSource.Cancel();
            args.Handled = true;
        });

        _keyboardHandler.RegisterKeyBinding(ConsoleKey.L, ConsoleModifiers.Control, args =>
        {
            _logger.LogDebug("Ctrl+L detected, clearing screen");
            _historyManager.ClearHistory();
            _scrollbackTerminal.Initialize();
            RenderInitialContent();
            args.Handled = true;
        });

        _keyboardHandler.RegisterKeyBinding(ConsoleKey.P, ConsoleModifiers.Control, args =>
        {
            NavigateCommandHistory(up: true);
            args.Handled = true;
        });

        _keyboardHandler.RegisterKeyBinding(ConsoleKey.N, ConsoleModifiers.Control, args =>
        {
            NavigateCommandHistory(up: false);
            args.Handled = true;
        });

        _keyboardHandler.RegisterKeyBinding(ConsoleKey.E, ConsoleModifiers.Control, args =>
        {
            _logger.LogDebug("Ctrl+E detected - external editor feature placeholder");
            args.Handled = true;
        });

        _logger.LogDebug("FlexColumn key bindings registered");
    }

    private async void OnKeyPressed(object? sender, KeyPressEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        try
        {
            await _stateManager.HandleKeyPressAsync(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling key press: {Key}", e.Key);
        }
    }

    private async void OnKeyCombinationPressed(object? sender, KeyCombinationEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Unhandled key combination: {Key} + {Modifiers}", e.Key, e.Modifiers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling key combination: {Key} + {Modifiers}", e.Key, e.Modifiers);
        }

        await Task.CompletedTask;
    }

    private async void OnCharacterTyped(object? sender, CharacterTypedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        try
        {
            await _stateManager.HandleCharacterTypedAsync(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling character typed: {Character}", e.Character);
        }
    }

    private async Task ShutdownAsync()
    {
        _logger.LogDebug("Shutting down FlexColumn TUI application");

        try
        {
            await _keyboardHandler.StopAsync();
            _scrollbackTerminal.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shutdown");
        }

        _logger.LogDebug("FlexColumn TUI application shutdown complete");
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _cancellationTokenSource.Cancel();
        _keyboardHandler?.Dispose();
        _cancellationTokenSource?.Dispose();
        _tuiContext.AiOperationCts?.Dispose();

        Started = null;
        Stopped = null;
        UnhandledError = null;

        _logger?.LogDebug("FlexColumnTuiApp disposed");

        GC.SuppressFinalize(this);
    }

    private void LoadCommandHistory()
    {
        try
        {
            var chatHistory = _historyManager.GetCurrentChatHistory();
            var userMessages = chatHistory
                .Where(m => m.Role == ChatRole.User)
                .Select(m => m.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            _tuiContext.CommandHistory.Clear();
            _tuiContext.CommandHistory.AddRange(userMessages);

            _logger?.LogDebug("Loaded {Count} commands from history", _tuiContext.CommandHistory.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error loading command history");
        }
    }

    private void NavigateCommandHistory(bool up)
    {
        if (_tuiContext.CommandHistory.Count == 0)
        {
            return;
        }

        var inputContext = _tuiContext.InputContext;

        if (up)
        {
            if (_tuiContext.CommandHistoryIndex == -1)
            {
                _tuiContext.CommandHistoryIndex = _tuiContext.CommandHistory.Count - 1;
                inputContext.CurrentInput = _tuiContext.CommandHistory[_tuiContext.CommandHistoryIndex];
            }
            else if (_tuiContext.CommandHistoryIndex > 0)
            {
                _tuiContext.CommandHistoryIndex--;
                inputContext.CurrentInput = _tuiContext.CommandHistory[_tuiContext.CommandHistoryIndex];
            }
        }
        else
        {
            if (_tuiContext.CommandHistoryIndex >= 0 && _tuiContext.CommandHistoryIndex < _tuiContext.CommandHistory.Count - 1)
            {
                _tuiContext.CommandHistoryIndex++;
                inputContext.CurrentInput = _tuiContext.CommandHistory[_tuiContext.CommandHistoryIndex];
            }
            else if (_tuiContext.CommandHistoryIndex == _tuiContext.CommandHistory.Count - 1)
            {
                _tuiContext.CommandHistoryIndex = -1;
                inputContext.CurrentInput = string.Empty;
            }
        }

        inputContext.CursorPosition = inputContext.CurrentInput.Length;
    }



}
