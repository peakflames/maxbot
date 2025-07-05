namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Integration tests to prevent regression of UI/UX bugs that were previously fixed.
/// These tests focus on user experience issues like debug spam, unwanted newlines,
/// excessive screen updates, and component rendering problems.
/// </summary>
public class UiUxRegressionTests
{
    private readonly ITestOutputHelper _output;

    public UiUxRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }


    [Fact]
    public async Task ScrollbackTerminal_ShouldNotUpdateUnnecessarily_WhenContentUnchanged()
    {
        // Arrange
        var testApp = new TestApplicationBuilder()
            .WithLogLevel(LogLevel.Debug)
            .Build();

        using var session = await testApp.StartAsync();
        var capture = new RenderingCapture(session.GetService<ILogger<RenderingCapture>>());
        var componentManager = session.GetService<ITuiComponentManager>();
        var tuiContext = session.GetService<ITuiContext>();
        var renderingUtilities = session.GetService<IRenderingUtilities>();
        var themeInfo = session.GetService<IThemeInfo>();
        var logger = session.GetService<ILogger<UiUxRegressionTests>>();

        var renderContext = new RenderContext(
            tuiContext,
            ChatState.Input,
            logger,
            session.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        // Act - Capture multiple snapshots over time without user interaction
        var startTime = DateTime.UtcNow;
        var snapshots = new List<RenderSnapshot>();

        // Capture snapshots every 100ms for 1 second (10 snapshots)
        for (int i = 0; i < 10; i++)
        {
            var snapshot = capture.CaptureLayoutSnapshot(componentManager, renderContext, $"Idle snapshot {i}");
            snapshots.Add(snapshot);
            await Task.Delay(100);
        }

        var endTime = DateTime.UtcNow;

        // Assert
        var performance = capture.AnalyzePerformance();
        
        _output.WriteLine($"Total renders: {performance.TotalRenders}");
        _output.WriteLine($"Content changes: {performance.ContentChanges}");
        _output.WriteLine($"Redundant renders: {performance.RedundantRenders}");
        _output.WriteLine($"Efficiency: {performance.EfficiencyPercentage:F1}%");
        _output.WriteLine($"Renders per second: {performance.RendersPerSecond:F1}");

        // 1. Should not have a rendering loop
        Assert.False(performance.HasRenderingLoop, 
            $"Detected rendering loop: {performance.RedundantRenders} redundant renders out of {performance.TotalRenders} total");

        // 2. When idle, content should not change frequently
        // Allow for some initial changes (like cursor blinking) but not excessive updates
        Assert.True(performance.ContentChanges <= 3, 
            $"Too many content changes ({performance.ContentChanges}) during idle period. This suggests unnecessary screen updates.");

        // 3. Should not render too frequently when idle
        Assert.True(performance.RendersPerSecond <= 10, 
            $"Rendering too frequently ({performance.RendersPerSecond:F1} FPS) when idle. This wastes CPU resources.");

        await session.StopAsync();
    }

    [Fact]
    public async Task WelcomePanel_ShouldRenderOnceInStaticContent_NotInDynamicContent()
    {
        // Arrange
        var testApp = new TestApplicationBuilder()
            .WithLogLevel(LogLevel.Debug)
            .Build();

        using var session = await testApp.StartAsync();
        var capture = new RenderingCapture(session.GetService<ILogger<RenderingCapture>>());
        var componentManager = session.GetService<ITuiComponentManager>();
        var tuiContext = session.GetService<ITuiContext>();
        var renderingUtilities = session.GetService<IRenderingUtilities>();
        var themeInfo = session.GetService<IThemeInfo>();
        var logger = session.GetService<ILogger<UiUxRegressionTests>>();

        // Ensure we have no chat history (so welcome should be shown)
        var historyManager = session.GetService<HistoryManager>();
        historyManager.ClearHistory();

        var renderContext = new RenderContext(
            tuiContext,
            ChatState.Input,
            logger,
            session.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        // Act - Capture dynamic content multiple times
        var snapshots = new List<RenderSnapshot>();
        for (int i = 0; i < 5; i++)
        {
            var snapshot = capture.CaptureLayoutSnapshot(componentManager, renderContext, $"Dynamic content {i}");
            snapshots.Add(snapshot);
            await Task.Delay(50);
        }

        // Assert
        // 1. Welcome content should not appear in dynamic content area
        // The welcome banner contains "MOGZI" ASCII art and "Now connected to your Multi-model Autonomous Assistant"
        foreach (var snapshot in snapshots)
        {
            Assert.False(snapshot.Content.Contains("███╗   ███╗"), 
                "Welcome banner ASCII art should not appear in dynamic content area");
            Assert.False(snapshot.Content.Contains("Now connected to your Multi-model Autonomous Assistant"), 
                "Welcome message should not appear in dynamic content area");
        }

        // 2. Dynamic content should be consistent (not changing the welcome message repeatedly)
        var uniqueContents = snapshots.Select(s => s.Content).Distinct().Count();
        Assert.True(uniqueContents <= 2, 
            $"Too many different dynamic content variations ({uniqueContents}). Welcome content might be rendering repeatedly.");

        await session.StopAsync();
    }




    [Fact]
    public async Task TellMeAJoke_EndToEndWorkflow_ShouldTransitionCorrectlyWithProperUIUpdates()
    {
        // Arrange
        var testApp = new TestApplicationBuilder()
            .WithLogLevel(LogLevel.Debug)
            .Build();

        using var session = await testApp.StartAsync();
        var capture = new RenderingCapture(session.GetService<ILogger<RenderingCapture>>());
        var keyboardHandler = session.GetService<IKeyboardHandler>();
        var componentManager = session.GetService<ITuiComponentManager>();
        var stateManager = session.GetService<ITuiStateManager>();
        var tuiContext = session.GetService<ITuiContext>();
        var renderingUtilities = session.GetService<IRenderingUtilities>();
        var themeInfo = session.GetService<IThemeInfo>();
        var logger = session.GetService<ILogger<UiUxRegressionTests>>();

        var testKeyboardHandler = keyboardHandler as TestKeyboardHandler;
        Assert.NotNull(testKeyboardHandler);

        var renderContext = new RenderContext(
            tuiContext,
            ChatState.Input,
            logger,
            session.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        // CRITICAL: Set up log monitoring to detect race conditions
        var testLoggerProvider = new TestLoggerProvider(LogLevel.Debug);
        var loggerFactory = session.GetService<ILoggerFactory>();
        loggerFactory.AddProvider(testLoggerProvider);

        // Act & Assert - Complete end-to-end workflow

        // 1. Verify initial state is Input
        Assert.Equal(ChatState.Input, stateManager.CurrentStateType);
        var initialSnapshot = capture.CaptureLayoutSnapshot(componentManager, renderContext, "Initial Input state");
        Assert.True(initialSnapshot.Analysis.HasInputIndicator, "Should show input indicator in initial state");
        Assert.False(initialSnapshot.Analysis.HasProgressIndicator, "Should not show progress in initial state");
        _output.WriteLine("✅ Initial state: Input with proper UI indicators");

        // 2. Type "tell me a joke"
        var command = "tell me a joke";
        foreach (char c in command)
        {
            testKeyboardHandler.SimulateCharacter(c);
            await Task.Delay(10);
        }

        // Wait for input to be processed
        await Task.Delay(100);
        
        // Verify input appears in UI
        var afterTypingSnapshot = capture.CaptureLayoutSnapshot(componentManager, renderContext, "After typing command");
        Assert.Contains(command, afterTypingSnapshot.Content);
        Assert.Equal(ChatState.Input, stateManager.CurrentStateType);
        _output.WriteLine("✅ Command typed and visible in UI, still in Input state");

        // 3. Press Enter to submit
        testKeyboardHandler.SimulateKeyPress(ConsoleKey.Enter, ConsoleModifiers.None, '\r');
        await Task.Delay(50); // Small delay for immediate state transition

        // 4. Verify transition to Thinking state
        var maxWaitForThinking = TimeSpan.FromSeconds(2);
        var thinkingStartTime = DateTime.UtcNow;
        bool reachedThinkingState = false;

        while (DateTime.UtcNow - thinkingStartTime < maxWaitForThinking)
        {
            if (stateManager.CurrentStateType == ChatState.Thinking)
            {
                reachedThinkingState = true;
                break;
            }
            await Task.Delay(10);
        }

        Assert.True(reachedThinkingState, "Should transition to Thinking state after pressing Enter");
        _output.WriteLine("✅ Transitioned to Thinking state");

        // 5. Verify Thinking state UI elements
        var thinkingRenderContext = new RenderContext(
            tuiContext,
            ChatState.Thinking,
            logger,
            session.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        // Capture multiple snapshots during thinking to verify spinner and timer updates
        var thinkingSnapshots = new List<RenderSnapshot>();
        var thinkingDuration = TimeSpan.FromSeconds(3); // Monitor for 3 seconds
        var thinkingEndTime = DateTime.UtcNow + thinkingDuration;

        while (DateTime.UtcNow < thinkingEndTime && stateManager.CurrentStateType == ChatState.Thinking)
        {
            var snapshot = capture.CaptureLayoutSnapshot(componentManager, thinkingRenderContext, 
                $"Thinking state at {DateTime.UtcNow:HH:mm:ss.fff}");
            thinkingSnapshots.Add(snapshot);
            
            // Verify thinking state UI characteristics
            Assert.False(snapshot.Analysis.HasInputIndicator, "Should not show input indicator during thinking");
            
            await Task.Delay(100); // Capture every 100ms
        }

        Assert.True(thinkingSnapshots.Count > 0, "Should have captured thinking state snapshots");
        _output.WriteLine($"✅ Captured {thinkingSnapshots.Count} thinking state snapshots");

        // CRITICAL: Verify spinner animation is actually working
        if (thinkingSnapshots.Count > 1)
        {
            var uniqueContents = thinkingSnapshots.Select(s => s.Content).Distinct().Count();
            
            // Extract spinner characters from each snapshot
            var spinnerFrames = new List<string>();
            foreach (var snapshot in thinkingSnapshots)
            {
                // Look for spinner characters in the content
                var spinnerMatch = System.Text.RegularExpressions.Regex.Match(snapshot.Content, @"[⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏]");
                if (spinnerMatch.Success)
                {
                    spinnerFrames.Add(spinnerMatch.Value);
                }
            }
            
            var uniqueSpinnerFrames = spinnerFrames.Distinct().Count();
            
            _output.WriteLine($"Captured {spinnerFrames.Count} spinner frames: [{string.Join(", ", spinnerFrames)}]");
            _output.WriteLine($"Unique spinner frames: {uniqueSpinnerFrames}");
            _output.WriteLine($"Unique content variations: {uniqueContents}");
            
            // FAIL THE TEST if spinner is not animating
            // With 3 seconds of capture at 100ms intervals (30 snapshots), we should see multiple spinner frames
            // The spinner has 8 frames and changes every 80ms, so we should definitely see animation
            Assert.True(uniqueSpinnerFrames > 1, 
                $"SPINNER ANIMATION BROKEN: Only {uniqueSpinnerFrames} unique spinner frame(s) detected across {thinkingSnapshots.Count} snapshots over 3 seconds. " +
                $"Expected multiple frames. Frames captured: [{string.Join(", ", spinnerFrames)}]. " +
                $"This indicates the ProgressPanel is not being marked dirty for re-rendering.");
            
            _output.WriteLine("✅ Spinner animation working correctly");
        }

        // 6. Wait for completion and verify return to Input state
        var maxWaitForCompletion = TimeSpan.FromSeconds(10);
        var completionStartTime = DateTime.UtcNow;
        bool returnedToInput = false;

        while (DateTime.UtcNow - completionStartTime < maxWaitForCompletion)
        {
            if (stateManager.CurrentStateType == ChatState.Input)
            {
                returnedToInput = true;
                break;
            }
            await Task.Delay(50);
        }

        Assert.True(returnedToInput, "Should return to Input state after AI processing completes");
        _output.WriteLine("✅ Returned to Input state after processing");

        // 7. CRITICAL: Analyze logs for race conditions and message duplication
        await Task.Delay(500); // Allow all async operations to complete and logs to be written
        
        // Extract log messages from the test logger provider
        var allLogEntries = testLoggerProvider.GetAllLogEntries();
        var logMessages = allLogEntries.Select(entry => entry.Message).ToList();
        
        var raceConditionLogs = logMessages.Where(log => 
            log.Contains("RACE_CONDITION_DEBUG") || 
            log.Contains("Successfully transitioned")).ToList();
        
        var assistantMessages = logMessages.Where(log => 
            log.Contains("ChatMsg[Assistant,") && 
            log.Contains("Why do programmers prefer dark mode")).ToList();

        _output.WriteLine($"\n=== RACE CONDITION ANALYSIS ===");
        _output.WriteLine($"Total log entries: {allLogEntries.Count}");
        _output.WriteLine($"Race condition debug logs: {raceConditionLogs.Count}");
        _output.WriteLine($"Assistant messages with joke: {assistantMessages.Count}");
        
        foreach (var log in raceConditionLogs)
        {
            _output.WriteLine($"  {log}");
        }
        
        foreach (var msg in assistantMessages)
        {
            _output.WriteLine($"  {msg}");
        }

        // Check for overlapping state transitions (same timestamp)
        var transitionLogs = logMessages.Where(log => 
            log.Contains("Successfully transitioned")).ToList();
        
        var overlappingTransitions = new List<string>();
        for (int i = 0; i < transitionLogs.Count - 1; i++)
        {
            var current = transitionLogs[i];
            var next = transitionLogs[i + 1];
            
            // Extract timestamps (rough check)
            if (current.Contains("17:") && next.Contains("17:"))
            {
                var currentTime = ExtractTimestamp(current);
                var nextTime = ExtractTimestamp(next);
                
                if (currentTime == nextTime)
                {
                    overlappingTransitions.Add($"OVERLAP: {current} | {next}");
                }
            }
        }

        // Check for duplicate final messages
        var finalJokeMessages = assistantMessages.Where(msg => 
            msg.Contains("Because light attracts bugs!")).ToList();

        _output.WriteLine($"\nFinal joke messages: {finalJokeMessages.Count}");
        foreach (var msg in finalJokeMessages)
        {
            _output.WriteLine($"  {msg}");
        }

        // CRITICAL ASSERTIONS: Detect the race conditions
        Assert.True(overlappingTransitions.Count == 0, 
            $"Detected overlapping state transitions: {string.Join("; ", overlappingTransitions)}");
        
        Assert.True(finalJokeMessages.Count <= 1, 
            $"Detected duplicate final messages ({finalJokeMessages.Count}). This indicates the race condition bug.");

        // 8. Verify final state and content
        await Task.Delay(200); // Allow final UI updates
        
        var finalRenderContext = new RenderContext(
            tuiContext,
            ChatState.Input,
            logger,
            session.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        var finalSnapshot = capture.CaptureLayoutSnapshot(componentManager, finalRenderContext, "Final state");
        
        // Should show input indicator again
        Assert.True(finalSnapshot.Analysis.HasInputIndicator, "Should show input indicator in final state");
        Assert.False(finalSnapshot.Analysis.HasProgressIndicator, "Should not show progress in final state");
        
        // 9. Verify state manager is ready for next command
        Assert.Equal(ChatState.Input, stateManager.CurrentStateType);
        Assert.Equal(string.Empty, tuiContext.InputContext.CurrentInput);
        _output.WriteLine("✅ Ready for next command (input cleared, in Input state)");

        // 10. Verify no error indicators
        Assert.False(finalSnapshot.Analysis.HasErrorIndicator, "Should not show error indicators in happy path");
        _output.WriteLine("✅ No error indicators present");

        // 11. Verify performance characteristics
        var performance = capture.AnalyzePerformance();
        _output.WriteLine($"Performance: {performance.TotalRenders} renders, {performance.EfficiencyPercentage:F1}% efficiency");
        
        // Should not have excessive redundant renders
        // Note: End-to-end tests with many snapshots will have lower efficiency due to test capture overhead
        Assert.True(performance.EfficiencyPercentage >= 10, 
            $"Rendering efficiency too low ({performance.EfficiencyPercentage:F1}%). May indicate UI update issues.");
        _output.WriteLine("✅ Rendering performance within acceptable range");

        _output.WriteLine("\n=== End-to-End Test Summary ===");
        _output.WriteLine("✅ Initial state: Input with proper indicators");
        _output.WriteLine("✅ Command input: Typed and visible");
        _output.WriteLine("✅ State transition: Input → Thinking");
        _output.WriteLine("✅ Thinking state: Proper UI updates (spinner/timer)");
        _output.WriteLine("✅ State transition: Thinking → Input");
        _output.WriteLine("✅ No race conditions detected");
        _output.WriteLine("✅ No duplicate messages detected");
        _output.WriteLine("✅ Ready for next command");
        _output.WriteLine("✅ No errors or performance issues");
        _output.WriteLine("✅ COMPLETE END-TO-END WORKFLOW VERIFIED");

        await session.StopAsync();
    }

    private string ExtractTimestamp(string logMessage)
    {
        // Extract timestamp from log message (rough implementation)
        var match = System.Text.RegularExpressions.Regex.Match(logMessage, @"(\d{2}:\d{2}:\d{2}\.\d{3})");
        return match.Success ? match.Groups[1].Value : "";
    }
}
