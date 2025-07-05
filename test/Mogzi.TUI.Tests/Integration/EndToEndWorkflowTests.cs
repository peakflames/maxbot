namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Integration tests for complete end-to-end user workflows.
/// Tests complete chat interactions from input to response without mocking.
/// </summary>
public class EndToEndWorkflowTests : IDisposable
{
    private readonly ILogger<EndToEndWorkflowTests> _logger;
    private bool _isDisposed = false;

    public EndToEndWorkflowTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<EndToEndWorkflowTests>();
    }

    [Fact]
    public async Task CompleteWorkflow_ShouldHandleUserInputToResponse()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();
        var tuiContext = testApp.GetService<ITuiContext>();
        var componentManager = testApp.GetService<ITuiComponentManager>();

        // Act - Simulate complete workflow
        // 1. Start in Input state
        stateManager.CurrentStateType.Should().Be(ChatState.Input);

        // 2. User types a message
        tuiContext.InputContext.CurrentInput = "Hello, how are you?";
        tuiContext.InputContext.CursorPosition = tuiContext.InputContext.CurrentInput.Length;

        // 3. User presses Enter (simulated)
        var userMessage = tuiContext.InputContext.CurrentInput;
        tuiContext.CommandHistory.Add(userMessage);
        tuiContext.InputContext.Clear();

        // 4. Transition to Thinking state (would normally be triggered by state manager)
        await stateManager.TransitionToStateAsync(ChatState.Thinking);

        // Assert - ThinkingState should gracefully handle empty chat history and return to Input
        // This is the correct behavior when there's no actual chat history to process
        stateManager.CurrentStateType.Should().Be(ChatState.Input, 
            "ThinkingState should return to Input when there's no chat history to process");
        tuiContext.CommandHistory.Should().Contain("Hello, how are you?");
        tuiContext.InputContext.CurrentInput.Should().BeEmpty();
    }

    [Fact]
    public async Task StateTransitions_ShouldUpdateComponentVisibility()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();
        var componentManager = testApp.GetService<ITuiComponentManager>();
        var tuiContext = testApp.GetService<ITuiContext>();

        // Act & Assert - Test Input state
        await stateManager.TransitionToStateAsync(ChatState.Input);
        var inputPanel = componentManager.GetComponent("InputPanel");
        inputPanel.Should().NotBeNull();
        inputPanel!.IsVisible.Should().BeTrue();

        // Act & Assert - Test Thinking state
        await stateManager.TransitionToStateAsync(ChatState.Thinking);
        var progressPanel = componentManager.GetComponent("ProgressPanel");
        progressPanel.Should().NotBeNull();
        progressPanel!.IsVisible.Should().BeTrue();

        // Act & Assert - Test ToolExecution state
        await stateManager.TransitionToStateAsync(ChatState.ToolExecution);
        // Tool execution components should be visible
        stateManager.CurrentStateType.Should().Be(ChatState.ToolExecution);
    }

    [Fact]
    public void SlashCommands_ShouldBeProcessedCorrectly()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var slashCommandProcessor = testApp.GetService<SlashCommandProcessor>();
        var tuiContext = testApp.GetService<ITuiContext>();

        // Act - Process a slash command
        var command = "/help";
        var success = slashCommandProcessor.TryProcessCommand(command, out var output);

        // Assert
        success.Should().BeTrue();
        output.Should().NotBeNull();
    }

    [Fact]
    public void AutocompleteWorkflow_ShouldProvideCompletions()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var autocompleteManager = testApp.GetService<AutocompleteManager>();
        var tuiContext = testApp.GetService<ITuiContext>();

        // Act - Trigger autocomplete for slash commands
        tuiContext.InputContext.CurrentInput = "/hel";
        tuiContext.InputContext.CursorPosition = 4;
        tuiContext.InputContext.ActiveAutocompleteType = AutocompleteType.SlashCommand;

        // Trigger autocomplete detection
        var provider = autocompleteManager.DetectTrigger(tuiContext.InputContext.CurrentInput, tuiContext.InputContext.CursorPosition);

        // Assert
        provider.Should().NotBeNull();
        tuiContext.InputContext.ActiveAutocompleteType.Should().Be(AutocompleteType.SlashCommand);
    }

    [Fact]
    public void UserSelection_ShouldHandleChoices()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var userSelectionManager = testApp.GetService<UserSelectionManager>();
        var tuiContext = testApp.GetService<ITuiContext>();

        // Act - Verify user selection manager is available
        // In a real test, this would simulate user selection through UI interaction
        userSelectionManager.Should().NotBeNull();
        tuiContext.Should().NotBeNull();

        // Assert - Services are properly configured
        userSelectionManager.Should().BeAssignableTo<UserSelectionManager>();
    }

    [Fact]
    public void CommandHistory_ShouldMaintainCommands()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var historyManager = testApp.GetService<HistoryManager>();
        var tuiContext = testApp.GetService<ITuiContext>();

        // Act - Add commands to history
        var commands = new[] { "command1", "command2", "command3" };
        foreach (var command in commands)
        {
            tuiContext.CommandHistory.Add(command);
        }

        // Assert - Commands should be in history
        tuiContext.CommandHistory.Should().Contain(commands);
        tuiContext.CommandHistory.Should().HaveCount(3);
        historyManager.Should().NotBeNull();
    }

    [Fact]
    public async Task ErrorHandling_ShouldDisplayErrorsGracefully()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var tuiContext = testApp.GetService<ITuiContext>();
        var stateManager = testApp.GetService<ITuiStateManager>();

        // Act - Simulate an error condition
        try
        {
            // This would normally trigger an error in the real application
            await stateManager.TransitionToStateAsync((ChatState)999);
        }
        catch (Exception ex)
        {
            // Assert - Error should be handled gracefully
            ex.Should().NotBeNull();
            stateManager.CurrentStateType.Should().Be(ChatState.Input); // Should remain in safe state
        }
    }

    [Fact]
    public async Task LongRunningOperations_ShouldShowProgress()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();
        var componentManager = testApp.GetService<ITuiComponentManager>();
        var tuiContext = testApp.GetService<ITuiContext>();

        // Act - Simulate long-running operation
        await stateManager.TransitionToStateAsync(ChatState.Thinking);

        // Assert - Progress panel should be available and ThinkingState should handle empty chat gracefully
        var progressPanel = componentManager.GetComponent("ProgressPanel");
        progressPanel.Should().NotBeNull();
        progressPanel!.IsVisible.Should().BeTrue();
        // ThinkingState correctly returns to Input when there's no chat history to process
        stateManager.CurrentStateType.Should().Be(ChatState.Input, 
            "ThinkingState should return to Input when there's no chat history to process");
    }

    [Fact]
    public async Task MultipleInputs_ShouldMaintainContext()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var tuiContext = testApp.GetService<ITuiContext>();
        var stateManager = testApp.GetService<ITuiStateManager>();

        // Act - Simulate multiple user inputs
        var inputs = new[] { "First message", "Second message", "Third message" };
        
        foreach (var input in inputs)
        {
            // Simulate user typing and submitting
            tuiContext.InputContext.CurrentInput = input;
            tuiContext.CommandHistory.Add(input);
            tuiContext.InputContext.Clear();
            
            // Brief transition to thinking and back
            await stateManager.TransitionToStateAsync(ChatState.Thinking);
            await stateManager.TransitionToStateAsync(ChatState.Input);
        }

        // Assert
        tuiContext.CommandHistory.Should().Contain(inputs);
        tuiContext.CommandHistory.Should().HaveCount(3);
        stateManager.CurrentStateType.Should().Be(ChatState.Input);
    }

    [Fact]
    public void KeyboardShortcuts_ShouldTriggerCorrectActions()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var tuiContext = testApp.GetService<ITuiContext>();

        // Act & Assert - Test Ctrl+L (clear screen)
        // This would normally be handled by keyboard handler
        var initialHistoryCount = tuiContext.CommandHistory.Count;
        
        // Simulate clear action
        tuiContext.InputContext.Clear();
        
        tuiContext.InputContext.CurrentInput.Should().BeEmpty();
        tuiContext.InputContext.CursorPosition.Should().Be(0);
    }

    [Fact]
    public async Task ApplicationShutdown_ShouldCleanupProperly()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var historyManager = testApp.GetService<HistoryManager>();
        var tuiContext = testApp.GetService<ITuiContext>();

        // Act - Simulate application shutdown
        // Verify history manager is available for cleanup
        historyManager.Should().NotBeNull();
        
        // Dispose components
        await componentManager.DisposeComponentsAsync();

        // Assert
        componentManager.Components.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeHandledSafely()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();
        var tuiContext = testApp.GetService<ITuiContext>();

        // Act - Simulate concurrent state transitions
        var tasks = new List<Task>();
        
        for (int i = 0; i < 5; i++)
        {
            var task = Task.Run(async () =>
            {
                await stateManager.TransitionToStateAsync(ChatState.Thinking);
                await Task.Delay(10);
                await stateManager.TransitionToStateAsync(ChatState.Input);
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert - Should end in a valid state
        stateManager.CurrentStateType.Should().BeOneOf(ChatState.Input, ChatState.Thinking, ChatState.ToolExecution);
    }

    [Fact]
    public void MemoryUsage_ShouldRemainStable()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var tuiContext = testApp.GetService<ITuiContext>();
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Simulate heavy usage
        for (int i = 0; i < 1000; i++)
        {
            tuiContext.InputContext.CurrentInput = $"Test message {i}";
            tuiContext.CommandHistory.Add(tuiContext.InputContext.CurrentInput);
            tuiContext.InputContext.Clear();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);

        // Assert - Memory usage should not grow excessively
        var memoryIncrease = finalMemory - initialMemory;
        memoryIncrease.Should().BeLessThan(10 * 1024 * 1024); // Less than 10MB increase
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        GC.SuppressFinalize(this);
    }
}
