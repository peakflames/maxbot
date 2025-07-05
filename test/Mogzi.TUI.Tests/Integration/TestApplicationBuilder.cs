namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Builder for creating real FlexColumnTuiApp instances for integration testing.
/// Uses actual service configuration without mocking to test real application behavior.
/// </summary>
public class TestApplicationBuilder
{
    private string? _configPath;
    private string? _profileName;
    private string? _toolApprovals;
    private LogLevel _logLevel = LogLevel.Debug;
    private readonly List<Action<IServiceCollection>> _serviceOverrides = new();

    /// <summary>
    /// Initializes a new instance of TestApplicationBuilder with the user's configuration file and testing profile.
    /// </summary>
    public TestApplicationBuilder()
    {
        // Default to using the user's existing mogzi.config.json file with the 'testing' profile
        _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "mogzi.config.json");
        _profileName = "testing";
    }

    /// <summary>
    /// Sets the configuration path for the test application.
    /// If not set, will use the default mogzi.config.json from user's home directory.
    /// </summary>
    public TestApplicationBuilder WithConfigPath(string configPath)
    {
        _configPath = configPath;
        return this;
    }

    /// <summary>
    /// Sets the profile name for the test application.
    /// </summary>
    public TestApplicationBuilder WithProfile(string profileName)
    {
        _profileName = profileName;
        return this;
    }

    /// <summary>
    /// Sets the tool approvals setting for the test application.
    /// </summary>
    public TestApplicationBuilder WithToolApprovals(string toolApprovals)
    {
        _toolApprovals = toolApprovals;
        return this;
    }

    /// <summary>
    /// Sets the logging level for the test application.
    /// </summary>
    public TestApplicationBuilder WithLogLevel(LogLevel logLevel)
    {
        _logLevel = logLevel;
        return this;
    }

    /// <summary>
    /// Adds a service override for testing purposes.
    /// This allows replacing specific services while keeping the rest of the real configuration.
    /// </summary>
    public TestApplicationBuilder WithServiceOverride(Action<IServiceCollection> serviceOverride)
    {
        _serviceOverrides.Add(serviceOverride);
        return this;
    }

    /// <summary>
    /// Builds a real FlexColumnTuiApp instance with test configuration.
    /// </summary>
    public TestApplication Build()
    {
        var services = new ServiceCollection();
        
        // Configure services using the real service configuration
        ServiceConfiguration.ConfigureServices(services, _configPath, _profileName, _toolApprovals);
        
        // Apply any service overrides for testing
        foreach (var serviceOverride in _serviceOverrides)
        {
            serviceOverride(services);
        }
        
        // Override with test-friendly services
        services.RemoveAll<IKeyboardHandler>();
        services.AddSingleton<IKeyboardHandler, TestKeyboardHandler>();
        
        // Override logging to capture test logs (only if not already overridden)
        if (!services.Any(s => s.ServiceType == typeof(ILoggerFactory) && s.Lifetime == ServiceLifetime.Singleton))
        {
            services.AddSingleton<ILoggerFactory>(provider =>
            {
                var factory = LoggerFactory.Create(builder =>
                {
                    builder.AddProvider(new TestLoggerProvider(_logLevel));
                    builder.SetMinimumLevel(_logLevel);
                });
                return factory;
            });
        }
        
        var serviceProvider = services.BuildServiceProvider();
        var app = serviceProvider.GetRequiredService<FlexColumnTuiApp>();
        var logger = serviceProvider.GetRequiredService<ILogger<TestApplication>>();
        
        // Initialize the TuiStateManager for testing
        // Note: We need to manually register state factories and initialize the state manager
        // because the real app does this in FlexColumnTuiApp.Initialize() which we don't call in tests
        InitializeStateManager(serviceProvider);
        
        return new TestApplication(app, serviceProvider, logger);
    }

    /// <summary>
    /// Initializes the TuiStateManager with state factories and context for testing.
    /// This mimics what FlexColumnTuiApp.Initialize() does.
    /// </summary>
    private static void InitializeStateManager(IServiceProvider serviceProvider)
    {
        var stateManager = serviceProvider.GetRequiredService<ITuiStateManager>();
        var tuiContext = serviceProvider.GetRequiredService<ITuiContext>();
        
        // Register state factories (exactly same as FlexColumnTuiApp.RegisterStateFactories())
        // Use method group syntax, not lambda expressions, to match the real application
        stateManager.RegisterState(ChatState.Input, serviceProvider.GetRequiredService<InputTuiState>);
        stateManager.RegisterState(ChatState.Thinking, serviceProvider.GetRequiredService<ThinkingTuiState>);
        stateManager.RegisterState(ChatState.ToolExecution, serviceProvider.GetRequiredService<ToolExecutionTuiState>);
        
        // Initialize the state manager with context (async call made synchronous for testing)
        stateManager.InitializeAsync(tuiContext).GetAwaiter().GetResult();
    }
}

/// <summary>
/// Wrapper for a test application instance that provides additional testing capabilities.
/// </summary>
public class TestApplication : IDisposable
{
    private readonly FlexColumnTuiApp _app;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TestApplication> _logger;
    private bool _isDisposed = false;

    public TestApplication(FlexColumnTuiApp app, IServiceProvider serviceProvider, ILogger<TestApplication> logger)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the underlying FlexColumnTuiApp instance.
    /// </summary>
    public FlexColumnTuiApp App => _app;

    /// <summary>
    /// Gets the service provider for accessing services.
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets a service from the dependency injection container.
    /// </summary>
    public T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a service from the dependency injection container, or null if not found.
    /// </summary>
    public T? GetOptionalService<T>() where T : class
    {
        return _serviceProvider.GetService<T>();
    }

    /// <summary>
    /// Starts the application in a background task for testing.
    /// </summary>
    public async Task<TestApplicationSession> StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting test application session");
        
        var session = new TestApplicationSession(_app, _serviceProvider, _logger);
        await session.StartAsync(cancellationToken);
        
        return session;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        _app?.Dispose();
        
        if (_serviceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents an active test application session.
/// </summary>
public class TestApplicationSession : IDisposable
{
    private readonly FlexColumnTuiApp _app;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TestApplication> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task? _appTask;
    private bool _isDisposed = false;

    public TestApplicationSession(FlexColumnTuiApp app, IServiceProvider serviceProvider, ILogger<TestApplication> logger)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Gets whether the application is currently running.
    /// </summary>
    public bool IsRunning => _app.IsRunning;

    /// <summary>
    /// Gets the service provider for accessing services.
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets a service from the dependency injection container.
    /// </summary>
    public T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Starts the application in a background task.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_appTask != null)
        {
            throw new InvalidOperationException("Application session is already started");
        }

        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token);

        _logger.LogDebug("Starting application in background task");
        
        _appTask = Task.Run(async () =>
        {
            try
            {
                var exitCode = await _app.RunAsync(Array.Empty<string>(), combinedCts.Token);
                _logger.LogDebug("Application completed with exit code: {ExitCode}", exitCode);
                return exitCode;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Application cancelled");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application failed with exception");
                throw;
            }
        }, combinedCts.Token);

        // Wait a short time for the application to start
        await Task.Delay(100, cancellationToken);
        
        // Verify the application started successfully
        if (_appTask.IsCompleted)
        {
            // If the task completed immediately, there might be an error
            try
            {
                await _appTask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Application failed to start", ex);
            }
        }
    }

    /// <summary>
    /// Stops the application session.
    /// </summary>
    public async Task StopAsync(TimeSpan? timeout = null)
    {
        if (_appTask == null)
        {
            return;
        }

        _logger.LogDebug("Stopping application session");
        
        _cancellationTokenSource.Cancel();
        
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(5);
        
        try
        {
            await _appTask.WaitAsync(timeoutValue);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Application did not stop within timeout period");
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        
        // Don't wait for the app task in Dispose to avoid blocking
        // The caller should use StopAsync for graceful shutdown
        
        GC.SuppressFinalize(this);
    }
}
