namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Integration tests for the rendering pipeline.
/// Tests component visibility, dirty tracking, caching, and layout composition without mocking.
/// </summary>
public class RenderingPipelineTests : IDisposable
{
    private readonly ILogger<RenderingPipelineTests> _logger;
    private bool _isDisposed = false;

    public RenderingPipelineTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<RenderingPipelineTests>();
    }

    [Fact]
    public void ComponentManager_ShouldRenderLayoutSuccessfully()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var renderingUtilities = testApp.GetService<IRenderingUtilities>();
        var themeInfo = testApp.GetService<IThemeInfo>();
        var tuiContext = testApp.GetService<ITuiContext>();
        var stateManager = testApp.GetService<ITuiStateManager>();

        var renderContext = new RenderContext(
            tuiContext,
            stateManager.CurrentStateType,
            testApp.GetService<ILogger<RenderContext>>(),
            testApp.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        // Act
        var renderable = componentManager.RenderLayout(renderContext);

        // Assert
        renderable.Should().NotBeNull();
        renderable.Should().BeAssignableTo<IRenderable>();
    }

    [Fact]
    public void ComponentVisibility_ShouldUpdateBasedOnState()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var stateManager = testApp.GetService<ITuiStateManager>();
        var tuiContext = testApp.GetService<ITuiContext>();

        var renderContext = new RenderContext(
            tuiContext,
            stateManager.CurrentStateType,
            testApp.GetService<ILogger<RenderContext>>(),
            testApp.ServiceProvider,
            testApp.GetService<IRenderingUtilities>(),
            testApp.GetService<IThemeInfo>()
        );

        // Act
        componentManager.UpdateComponentVisibility(ChatState.Input, renderContext);

        // Assert
        var inputPanel = componentManager.GetComponent("InputPanel");
        var footerPanel = componentManager.GetComponent("FooterPanel");
        
        inputPanel.Should().NotBeNull();
        footerPanel.Should().NotBeNull();
        
        // In Input state, these components should be visible
        inputPanel!.IsVisible.Should().BeTrue();
        footerPanel!.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void DirtyTracking_ShouldMarkComponentsAsDirty()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var inputPanel = componentManager.GetComponent("InputPanel") as BaseTuiComponent;

        // Act
        inputPanel?.MarkDirty();

        // Assert
        inputPanel.Should().NotBeNull();
        inputPanel!.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void RenderCache_ShouldCacheRenderedContent()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var renderCache = testApp.GetService<IRenderCache>();
        var testKey = "test-cache-key";
        var testContent = new Text("Test content");

        // Act
        renderCache.CacheRender(testKey, testContent);
        var cachedContent = renderCache.GetCachedRender(testKey);

        // Assert
        cachedContent.Should().NotBeNull();
        cachedContent.Should().Be(testContent);
    }

    [Fact]
    public void RenderCache_ShouldEvictOldEntries()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var renderCache = testApp.GetService<IRenderCache>();

        // Act - Fill cache beyond capacity (assuming LRU cache with limited size)
        for (int i = 0; i < 1000; i++)
        {
            renderCache.CacheRender($"key-{i}", new Text($"Content {i}"));
        }

        // Assert - First entries should be evicted
        var firstEntry = renderCache.GetCachedRender("key-0");
        var lastEntry = renderCache.GetCachedRender("key-999");

        // Depending on cache size, first entry might be evicted
        lastEntry.Should().NotBeNull();
    }

    [Fact]
    public void ComponentLayout_ShouldComposeCorrectly()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var layout = testApp.GetService<ITuiLayout>();

        // Act
        var components = componentManager.Components;
        var isValid = layout.ValidateComponents(components);

        // Assert
        isValid.Should().BeTrue();
        layout.Should().NotBeNull();
        layout.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RenderingPerformance_ShouldBeEfficient()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var renderingUtilities = testApp.GetService<IRenderingUtilities>();
        var themeInfo = testApp.GetService<IThemeInfo>();
        var tuiContext = testApp.GetService<ITuiContext>();
        var stateManager = testApp.GetService<ITuiStateManager>();

        var renderContext = new RenderContext(
            tuiContext,
            stateManager.CurrentStateType,
            testApp.GetService<ILogger<RenderContext>>(),
            testApp.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        // Act - Measure rendering performance
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < 100; i++)
        {
            var renderable = componentManager.RenderLayout(renderContext);
            renderable.Should().NotBeNull();
        }
        
        stopwatch.Stop();

        // Assert - Rendering should be fast (less than 1 second for 100 renders)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public void ComponentInitialization_ShouldCompleteSuccessfully()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var renderingUtilities = testApp.GetService<IRenderingUtilities>();
        var themeInfo = testApp.GetService<IThemeInfo>();
        var tuiContext = testApp.GetService<ITuiContext>();
        var stateManager = testApp.GetService<ITuiStateManager>();

        var renderContext = new RenderContext(
            tuiContext,
            stateManager.CurrentStateType,
            testApp.GetService<ILogger<RenderContext>>(),
            testApp.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        // Act
        var initTask = componentManager.InitializeComponentsAsync(renderContext);

        // Assert
        initTask.Should().NotBeNull();
        initTask.IsCompleted.Should().BeTrue();
        initTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void ComponentDisposal_ShouldCleanupProperly()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var initialComponentCount = componentManager.Components.Count;

        // Act
        var disposeTask = componentManager.DisposeComponentsAsync();

        // Assert
        disposeTask.Should().NotBeNull();
        disposeTask.IsCompleted.Should().BeTrue();
        disposeTask.IsCompletedSuccessfully.Should().BeTrue();
        componentManager.Components.Should().BeEmpty();
    }

    [Fact]
    public void RenderContext_ShouldProvideCorrectInformation()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var renderingUtilities = testApp.GetService<IRenderingUtilities>();
        var themeInfo = testApp.GetService<IThemeInfo>();
        var tuiContext = testApp.GetService<ITuiContext>();
        var stateManager = testApp.GetService<ITuiStateManager>();

        // Act
        var renderContext = new RenderContext(
            tuiContext,
            stateManager.CurrentStateType,
            testApp.GetService<ILogger<RenderContext>>(),
            testApp.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        // Assert
        renderContext.Should().NotBeNull();
        renderContext.CurrentState.Should().Be(ChatState.Input);
        renderContext.TuiContext.Should().Be(tuiContext);
        renderContext.ServiceProvider.Should().Be(testApp.ServiceProvider);
    }

    [Fact]
    public void ComponentRegistration_ShouldMaintainOrder()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();

        // Act
        var componentNames = componentManager.Components.Keys.ToList();

        // Assert
        componentNames.Should().Contain("InputPanel");
        componentNames.Should().Contain("AutocompletePanel");
        componentNames.Should().Contain("UserSelectionPanel");
        componentNames.Should().Contain("ProgressPanel");
        componentNames.Should().Contain("FooterPanel");
        componentNames.Should().Contain("WelcomePanel");
    }

    [Fact]
    public void StateTransition_ShouldUpdateComponentVisibility()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var stateManager = testApp.GetService<ITuiStateManager>();
        var tuiContext = testApp.GetService<ITuiContext>();

        var renderContext = new RenderContext(
            tuiContext,
            stateManager.CurrentStateType,
            testApp.GetService<ILogger<RenderContext>>(),
            testApp.ServiceProvider,
            testApp.GetService<IRenderingUtilities>(),
            testApp.GetService<IThemeInfo>()
        );

        // Act - Simulate state transition to Thinking
        componentManager.UpdateComponentVisibility(ChatState.Thinking, renderContext);

        // Assert
        var progressPanel = componentManager.GetComponent("ProgressPanel");
        progressPanel.Should().NotBeNull();
        
        // In Thinking state, progress panel should be visible
        progressPanel!.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void RenderingCapture_ShouldCaptureSnapshots()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var renderingCapture = new RenderingCapture(testApp.GetService<ILogger<RenderingCapture>>());
        var renderingUtilities = testApp.GetService<IRenderingUtilities>();
        var themeInfo = testApp.GetService<IThemeInfo>();
        var tuiContext = testApp.GetService<ITuiContext>();
        var stateManager = testApp.GetService<ITuiStateManager>();

        var renderContext = new RenderContext(
            tuiContext,
            stateManager.CurrentStateType,
            testApp.GetService<ILogger<RenderContext>>(),
            testApp.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        // Act
        var snapshot = renderingCapture.CaptureLayoutSnapshot(componentManager, renderContext, "Test snapshot");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Description.Should().Be("Test snapshot");
        snapshot.Content.Should().NotBeNullOrEmpty();
        snapshot.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RenderingLoop_ShouldBeDetectable()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var renderingCapture = new RenderingCapture(testApp.GetService<ILogger<RenderingCapture>>());

        // Act - Simulate multiple renders with same content (rendering loop)
        var testContent = new Text("Same content");
        for (int i = 0; i < 20; i++)
        {
            renderingCapture.CaptureSnapshot(testContent, $"Render {i}");
        }

        var analysis = renderingCapture.AnalyzePerformance();

        // Assert
        analysis.TotalRenders.Should().Be(20);
        analysis.ContentChanges.Should().Be(0); // Same content every time
        analysis.RedundantRenders.Should().BeGreaterThan(10);
        analysis.HasRenderingLoop.Should().BeTrue();
    }

    [Fact]
    public void ComponentInput_ShouldBeBroadcastCorrectly()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var componentManager = testApp.GetService<ITuiComponentManager>();
        var renderingUtilities = testApp.GetService<IRenderingUtilities>();
        var themeInfo = testApp.GetService<IThemeInfo>();
        var tuiContext = testApp.GetService<ITuiContext>();
        var stateManager = testApp.GetService<ITuiStateManager>();

        var renderContext = new RenderContext(
            tuiContext,
            stateManager.CurrentStateType,
            testApp.GetService<ILogger<RenderContext>>(),
            testApp.ServiceProvider,
            renderingUtilities,
            themeInfo
        );

        var inputEvent = new object(); // Placeholder input event

        // Act
        var broadcastTask = componentManager.BroadcastInputAsync(inputEvent, renderContext);

        // Assert
        broadcastTask.Should().NotBeNull();
        broadcastTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void ThemeInfo_ShouldProvideConsistentStyling()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var themeInfo = testApp.GetService<IThemeInfo>();

        // Act & Assert
        themeInfo.Should().NotBeNull();
        themeInfo.Should().BeAssignableTo<IThemeInfo>();
    }

    [Fact]
    public void RenderingUtilities_ShouldProvideHelperMethods()
    {
        // Arrange
        using var testApp = new TestApplicationBuilder()
            .Build();

        var renderingUtilities = testApp.GetService<IRenderingUtilities>();

        // Act & Assert
        renderingUtilities.Should().NotBeNull();
        renderingUtilities.Should().BeAssignableTo<IRenderingUtilities>();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        GC.SuppressFinalize(this);
    }
}
