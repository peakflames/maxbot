namespace Mogzi.TUI.Infrastructure;

/// <summary>
/// Shared service configuration for dependency injection.
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    /// Configures the dependency injection container.
    /// </summary>
    public static void ConfigureServices(IServiceCollection services, string? configPath = null, string? profileName = null, string? toolApprovals = null)
    {
        // Add logging - file logging only to keep UI clean
        _ = services.AddLogging(builder =>
        {
            _ = builder.AddProvider(new FileLoggerProvider(LogLevel.Debug));
            _ = builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add Spectre.Console
        _ = services.AddSingleton(AnsiConsole.Console);

        // Add core services
        _ = services.AddSingleton<IWorkingDirectoryProvider, DefaultWorkingDirectoryProvider>();

        // Create ChatClient - this will be configured per command based on settings
        var chatClientResult = ChatClient.Create(
            configPath, // Let Create handle finding the default path
            profileName, // Use specified profile or default
            toolApprovals, // Use specified tool approvals override
            "chat",
            (details, color) => { },
            false
        );

        _ = chatClientResult.IsSuccess
            ? services.AddSingleton(chatClientResult.Value)
            : throw new InvalidOperationException($"Failed to create ChatClient: {string.Join(", ", chatClientResult.Errors.Select(e => e.Message))}");

        _ = services.AddSingleton<IAppService, AppService>();
        _ = services.AddSingleton<HistoryManager>();
        _ = services.AddSingleton<StateManager>();

        // Add autocomplete services
        _ = services.AddSingleton<SlashCommandProcessor>();
        _ = services.AddSingleton<IAutocompleteProvider, SlashCommandProvider>();
        _ = services.AddSingleton<IAutocompleteProvider, FilePathProvider>();
        _ = services.AddSingleton<AutocompleteManager>();

        // Add user selection services
        _ = services.AddSingleton<IUserSelectionProvider, ToolApprovalsProvider>();
        _ = services.AddSingleton<UserSelectionManager>();

        // Add TUI infrastructure components
        _ = services.AddSingleton<FlexColumnTuiApp>();
        _ = services.AddSingleton<IScrollbackTerminal, ScrollbackTerminal>();
        _ = services.AddSingleton<IKeyboardHandler, AdvancedKeyboardHandler>();
        _ = services.AddSingleton<ToolResponseParser>();

        // Add state management components
        _ = services.AddSingleton<ITuiStateManager, TuiStateManager>();
        _ = services.AddSingleton<InputContext>();
        _ = services.AddSingleton<ITuiContext>(serviceProvider =>
        {
            var inputContext = serviceProvider.GetRequiredService<InputContext>();
            var logger = serviceProvider.GetRequiredService<ILogger<TuiContext>>();
            var scrollbackTerminal = serviceProvider.GetRequiredService<IScrollbackTerminal>();
            var historyManager = serviceProvider.GetRequiredService<HistoryManager>();
            var autocompleteManager = serviceProvider.GetRequiredService<AutocompleteManager>();
            var userSelectionManager = serviceProvider.GetRequiredService<UserSelectionManager>();
            var slashCommandProcessor = serviceProvider.GetRequiredService<SlashCommandProcessor>();
            var workingDirectoryProvider = serviceProvider.GetRequiredService<IWorkingDirectoryProvider>();
            var toolResponseParser = serviceProvider.GetRequiredService<ToolResponseParser>();
            var appService = serviceProvider.GetRequiredService<IAppService>();
            var stateManager = serviceProvider.GetRequiredService<ITuiStateManager>();

            return new TuiContext(
                inputContext,
                serviceProvider,
                logger,
                scrollbackTerminal,
                historyManager,
                autocompleteManager,
                userSelectionManager,
                slashCommandProcessor,
                workingDirectoryProvider,
                toolResponseParser,
                appService,
                stateManager);
        });

        // Register state implementations
        _ = services.AddTransient<InputTuiState>();
        _ = services.AddTransient<ThinkingTuiState>();
        _ = services.AddTransient<ToolExecutionTuiState>();

        // Register component architecture services
        _ = services.AddSingleton<IRenderingUtilities, RenderingUtilities>();
        _ = services.AddSingleton<IThemeInfo, DefaultThemeInfo>();
        _ = services.AddSingleton<ITuiLayout, FlexColumnLayout>();
        _ = services.AddSingleton<IRenderCache, TuiRenderCache>();

        // Register TUI components
        _ = services.AddSingleton<InputPanel>();
        _ = services.AddSingleton<AutocompletePanel>();
        _ = services.AddSingleton<UserSelectionPanel>();
        _ = services.AddSingleton<ProgressPanel>();
        _ = services.AddSingleton<FooterPanel>();
        _ = services.AddSingleton<WelcomePanel>();

        // Register and configure component manager
        _ = services.AddSingleton<ITuiComponentManager>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<TuiComponentManager>>();
            var renderCache = serviceProvider.GetRequiredService<IRenderCache>();
            var componentManager = new TuiComponentManager(logger, renderCache);
            var layout = serviceProvider.GetRequiredService<ITuiLayout>();

            // Register all components
            componentManager.RegisterComponent(serviceProvider.GetRequiredService<InputPanel>());
            componentManager.RegisterComponent(serviceProvider.GetRequiredService<AutocompletePanel>());
            componentManager.RegisterComponent(serviceProvider.GetRequiredService<UserSelectionPanel>());
            componentManager.RegisterComponent(serviceProvider.GetRequiredService<ProgressPanel>());
            componentManager.RegisterComponent(serviceProvider.GetRequiredService<FooterPanel>());
            componentManager.RegisterComponent(serviceProvider.GetRequiredService<WelcomePanel>());

            // Set the layout
            componentManager.CurrentLayout = layout;

            return componentManager;
        });

        // Register and configure mediator
        _ = services.AddSingleton<ITuiMediator>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<FlexColumnMediator>>();
            var mediator = new FlexColumnMediator(logger);

            // Register components with mediator
            mediator.RegisterComponent(serviceProvider.GetRequiredService<InputPanel>());
            mediator.RegisterComponent(serviceProvider.GetRequiredService<AutocompletePanel>());
            mediator.RegisterComponent(serviceProvider.GetRequiredService<UserSelectionPanel>());
            mediator.RegisterComponent(serviceProvider.GetRequiredService<ProgressPanel>());
            mediator.RegisterComponent(serviceProvider.GetRequiredService<FooterPanel>());
            mediator.RegisterComponent(serviceProvider.GetRequiredService<WelcomePanel>());

            return mediator;
        });
    }
}
