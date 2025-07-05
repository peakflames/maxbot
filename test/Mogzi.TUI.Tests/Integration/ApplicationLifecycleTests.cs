namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Integration tests for application lifecycle management.
/// Tests startup, shutdown, state transitions, and service initialization without mocking.
/// </summary>
public class ApplicationLifecycleTests : IDisposable
{
    private readonly ILogger<ApplicationLifecycleTests> _logger;
    private bool _isDisposed = false;

    public ApplicationLifecycleTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<ApplicationLifecycleTests>();
    }

    [Fact]
    public void Application_ShouldStartSuccessfully()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .WithLogLevel(LogLevel.Debug)
            .Build();

        // Act & Assert
        testApp.Should().NotBeNull();
        testApp.App.Should().NotBeNull();
        testApp.App.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Application_ShouldInitializeAllServices()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act - Get all required services
        var stateManager = testApp.GetService<ITuiStateManager>();
        var componentManager = testApp.GetService<ITuiComponentManager>();
        var tuiContext = testApp.GetService<ITuiContext>();
        var mediator = testApp.GetService<ITuiMediator>();

        // Assert
        stateManager.Should().NotBeNull();
        componentManager.Should().NotBeNull();
        tuiContext.Should().NotBeNull();
        mediator.Should().NotBeNull();
    }

    [Fact]
    public void Application_ShouldRegisterAllComponents()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        var componentManager = testApp.GetService<ITuiComponentManager>();
        var components = componentManager.Components;

        // Assert
        components.Should().NotBeEmpty();
        components.Should().ContainKey("InputPanel");
        components.Should().ContainKey("AutocompletePanel");
        components.Should().ContainKey("UserSelectionPanel");
        components.Should().ContainKey("ProgressPanel");
        components.Should().ContainKey("FooterPanel");
        components.Should().ContainKey("WelcomePanel");
    }

    [Fact]
    public void Application_ShouldInitializeInInputState()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        var stateManager = testApp.GetService<ITuiStateManager>();

        // Assert
        stateManager.CurrentStateType.Should().Be(ChatState.Input);
    }

    [Fact]
    public async Task Application_ShouldStartAndStopGracefully()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var startedEventFired = false;
        var stoppedEventFired = false;

        testApp.App.Started += () => startedEventFired = true;
        testApp.App.Stopped += () => stoppedEventFired = true;

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        
        var appTask = Task.Run(async () =>
        {
            try
            {
                return await testApp.App.RunAsync(Array.Empty<string>(), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        });

        // Wait a bit for startup
        await Task.Delay(500);
        
        // Cancel to trigger shutdown
        cts.Cancel();
        
        // Wait for completion
        var exitCode = await appTask;

        // Assert
        exitCode.Should().Be(0);
        startedEventFired.Should().BeTrue();
        stoppedEventFired.Should().BeTrue();
    }

    [Fact]
    public async Task Application_ShouldHandleMultipleStartupAttempts()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act & Assert
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task1 = Task.Run(() => testApp.App.RunAsync(Array.Empty<string>(), cts1.Token));

        await Task.Delay(100); // Let first startup begin

        // Second startup attempt should throw
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var act = () => testApp.App.RunAsync(Array.Empty<string>(), cts2.Token);
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already running*");

        // Cleanup
        cts1.Cancel();
        try { await task1; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task Application_ShouldInitializeKeyboardHandler()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        using var session = await testApp.StartAsync();
        await Task.Delay(200); // Allow initialization

        // Assert
        session.IsRunning.Should().BeTrue();
        
        // Stop the session
        await session.StopAsync();
    }

    [Fact]
    public void Application_ShouldInitializeComponentVisibility()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        var componentManager = testApp.GetService<ITuiComponentManager>();
        var stateManager = testApp.GetService<ITuiStateManager>();
        
        // Get components that should be visible in Input state
        var inputPanel = componentManager.GetComponent("InputPanel");
        var welcomePanel = componentManager.GetComponent("WelcomePanel");
        var footerPanel = componentManager.GetComponent("FooterPanel");

        // Assert
        inputPanel.Should().NotBeNull();
        welcomePanel.Should().NotBeNull();
        footerPanel.Should().NotBeNull();
        
        // In Input state, these components should be visible
        inputPanel!.IsVisible.Should().BeTrue();
        footerPanel!.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldLoadCommandHistory()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        var tuiContext = testApp.GetService<ITuiContext>();
        var historyManager = testApp.GetService<HistoryManager>();

        // Assert
        tuiContext.CommandHistory.Should().NotBeNull();
        historyManager.Should().NotBeNull();
        
        // Command history should be initialized (even if empty)
        tuiContext.CommandHistory.Should().BeOfType<List<string>>();
    }

    [Fact]
    public void Application_ShouldInitializeRenderCache()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        var renderCache = testApp.GetService<IRenderCache>();

        // Assert
        renderCache.Should().NotBeNull();
        renderCache.Should().BeAssignableTo<IRenderCache>();
    }

    [Fact]
    public void Application_ShouldHandleDisposalProperly()
    {
        // Arrange
        var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        testApp.Dispose();

        // Assert - Should not throw
        var act = () => testApp.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Application_ShouldInitializeLogging()
    {
        // Arrange
        var testLoggerProvider = new TestLoggerProvider(LogLevel.Debug);
        
        using var testApp = new TestApplicationBuilder()
            .WithServiceOverride(services =>
            {
                // Override the ILoggerFactory to use our test logger provider
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(provider =>
                {
                    var factory = LoggerFactory.Create(builder =>
                    {
                        builder.AddProvider(testLoggerProvider);
                        builder.SetMinimumLevel(LogLevel.Debug);
                    });
                    return factory;
                });
            })
            .Build();

        // Act
        var logger = testApp.GetService<ILogger<FlexColumnTuiApp>>();

        // Assert
        logger.Should().NotBeNull();
        
        // Verify logging is working
        logger.LogInformation("Test log message");
        var logEntries = testLoggerProvider.GetAllLogEntries();
        logEntries.Should().Contain(entry => entry.Message.Contains("Test log message"));
    }

    [Fact]
    public void Application_ShouldInitializeScrollbackTerminal()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        var scrollbackTerminal = testApp.GetService<IScrollbackTerminal>();

        // Assert
        scrollbackTerminal.Should().NotBeNull();
        scrollbackTerminal.Should().BeAssignableTo<IScrollbackTerminal>();
    }

    [Fact]
    public void Application_ShouldInitializeAutocompleteServices()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        var autocompleteManager = testApp.GetService<AutocompleteManager>();
        var slashCommandProcessor = testApp.GetService<SlashCommandProcessor>();

        // Assert
        autocompleteManager.Should().NotBeNull();
        slashCommandProcessor.Should().NotBeNull();
    }

    [Fact]
    public void Application_ShouldInitializeUserSelectionServices()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        // Act
        var userSelectionManager = testApp.GetService<UserSelectionManager>();

        // Assert
        userSelectionManager.Should().NotBeNull();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        GC.SuppressFinalize(this);
    }
}
