namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Tests for the core state management infrastructure.
/// These tests focus on state transitions, factory registration, and state manager behavior
/// independent of full application workflows.
/// </summary>
public class StateManagementTests : IDisposable
{
    private bool _isDisposed = false;

    [Fact]
    public void StateFactories_ShouldBeRegisteredCorrectly()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var serviceProvider = testApp.ServiceProvider;

        // Act: Try to get states directly from DI container
        var inputState = serviceProvider.GetRequiredService<InputTuiState>();
        var thinkingState = serviceProvider.GetRequiredService<ThinkingTuiState>();
        var toolExecutionState = serviceProvider.GetRequiredService<ToolExecutionTuiState>();

        // Assert: All states should be available and properly configured
        inputState.Should().NotBeNull();
        thinkingState.Should().NotBeNull();
        toolExecutionState.Should().NotBeNull();

        inputState.Name.Should().Be("Input");
        thinkingState.Name.Should().Be("Thinking");
        toolExecutionState.Name.Should().Be("ToolExecution");
    }

    [Fact]
    public void StateManager_ShouldInitializeToInputState()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();

        // Act & Assert: State manager should initialize to Input state
        stateManager.CurrentStateType.Should().Be(ChatState.Input);
        stateManager.CurrentState.Should().NotBeNull();
    }

    [Fact]
    public async Task StateTransition_WithEmptyHistory_ShouldHandleGracefully()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();
        var historyManager = testApp.GetService<HistoryManager>();

        // Ensure we start with empty history
        historyManager.ClearHistory();
        var chatHistory = historyManager.GetCurrentChatHistory();
        chatHistory.Should().BeEmpty();

        // Act: Try to transition to Thinking state with empty history
        await stateManager.TransitionToStateAsync(ChatState.Thinking);

        // Assert: Should gracefully return to Input state (ThinkingTuiState detects empty history)
        stateManager.CurrentStateType.Should().Be(ChatState.Input);
    }

    [Fact]
    public async Task StateTransition_WithValidHistory_ShouldTransitionCorrectly()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();
        var historyManager = testApp.GetService<HistoryManager>();

        // Add a user message to history
        var userMessage = new ChatMessage(ChatRole.User, "Test message");
        historyManager.AddUserMessage(userMessage);

        var chatHistory = historyManager.GetCurrentChatHistory();
        chatHistory.Should().NotBeEmpty();

        // Act: Try to transition to Thinking state with valid history
        await stateManager.TransitionToStateAsync(ChatState.Thinking);

        // Note: The state will likely transition back to Input quickly due to AI processing,
        // but the transition itself should succeed without throwing exceptions
        // This test verifies the transition mechanism works, not the AI processing outcome
        stateManager.CurrentStateType.Should().BeOneOf(ChatState.Input, ChatState.Thinking);
    }

    [Fact]
    public async Task StateTransition_ToToolExecution_ShouldWork()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();

        // Act: Transition to ToolExecution state
        await stateManager.TransitionToStateAsync(ChatState.ToolExecution);

        // Assert: Should be in ToolExecution state
        stateManager.CurrentStateType.Should().Be(ChatState.ToolExecution);
    }

    [Fact]
    public async Task StateTransition_BackToInput_ShouldWork()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();

        // Start in a different state
        await stateManager.TransitionToStateAsync(ChatState.ToolExecution);
        stateManager.CurrentStateType.Should().Be(ChatState.ToolExecution);

        // Act: Transition back to Input
        await stateManager.TransitionToStateAsync(ChatState.Input);

        // Assert: Should be back in Input state
        stateManager.CurrentStateType.Should().Be(ChatState.Input);
    }

    [Fact]
    public async Task StateManager_ShouldHandleMultipleTransitions()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();

        // Act: Perform multiple state transitions
        await stateManager.TransitionToStateAsync(ChatState.ToolExecution);
        stateManager.CurrentStateType.Should().Be(ChatState.ToolExecution);

        await stateManager.TransitionToStateAsync(ChatState.Input);
        stateManager.CurrentStateType.Should().Be(ChatState.Input);

        await stateManager.TransitionToStateAsync(ChatState.ToolExecution);
        stateManager.CurrentStateType.Should().Be(ChatState.ToolExecution);

        await stateManager.TransitionToStateAsync(ChatState.Input);
        stateManager.CurrentStateType.Should().Be(ChatState.Input);

        // Assert: Final state should be Input
        stateManager.CurrentStateType.Should().Be(ChatState.Input);
    }

    [Fact]
    public async Task StateManager_ShouldNotTransitionToSameState()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();
        
        // Ensure we're in Input state
        stateManager.CurrentStateType.Should().Be(ChatState.Input);

        // Act: Try to transition to the same state
        await stateManager.TransitionToStateAsync(ChatState.Input);

        // Assert: Should remain in Input state (no-op transition)
        stateManager.CurrentStateType.Should().Be(ChatState.Input);
    }

    [Fact]
    public void StateManager_ShouldProvideStateFactories()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var serviceProvider = testApp.ServiceProvider;
        var tuiStateManagerLogger = serviceProvider.GetRequiredService<ILogger<TuiStateManager>>();
        var tuiContext = testApp.GetService<ITuiContext>();

        // Act: Create a fresh state manager and register factories manually
        var testStateManager = new TuiStateManager(tuiStateManagerLogger);
        
        // Register state factories using the same pattern as the real application
        testStateManager.RegisterState(ChatState.Input, () => serviceProvider.GetRequiredService<InputTuiState>());
        testStateManager.RegisterState(ChatState.Thinking, () => serviceProvider.GetRequiredService<ThinkingTuiState>());
        testStateManager.RegisterState(ChatState.ToolExecution, () => serviceProvider.GetRequiredService<ToolExecutionTuiState>());

        // Initialize the state manager
        testStateManager.InitializeAsync(tuiContext).GetAwaiter().GetResult();

        // Assert: State manager should be properly initialized
        testStateManager.CurrentStateType.Should().Be(ChatState.Input);
        testStateManager.CurrentState.Should().NotBeNull();
    }

    [Fact]
    public async Task StateTransition_ShouldCallStateLifecycleMethods()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var stateManager = testApp.GetService<ITuiStateManager>();

        // Act: Transition between states to verify lifecycle methods are called
        // (This test verifies that OnEnterAsync and OnExitAsync are called without exceptions)
        
        await stateManager.TransitionToStateAsync(ChatState.ToolExecution);
        stateManager.CurrentStateType.Should().Be(ChatState.ToolExecution);

        await stateManager.TransitionToStateAsync(ChatState.Input);
        stateManager.CurrentStateType.Should().Be(ChatState.Input);

        // If we get here without exceptions, the lifecycle methods are working
        // Assert: Test completed successfully
        stateManager.CurrentStateType.Should().Be(ChatState.Input);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        GC.SuppressFinalize(this);
    }
}
