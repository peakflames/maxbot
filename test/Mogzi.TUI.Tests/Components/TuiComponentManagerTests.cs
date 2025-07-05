namespace Mogzi.TUI.Tests.Components;

public class TuiComponentManagerTests
{
    private readonly ILogger<TuiComponentManager> _logger;
    private readonly TuiComponentManager _componentManager;

    public TuiComponentManagerTests()
    {
        _logger = Substitute.For<ILogger<TuiComponentManager>>();
        var renderCache = Substitute.For<IRenderCache>();
        _componentManager = new TuiComponentManager(_logger, renderCache);
    }

    [Fact]
    public void RegisterComponent_ShouldAddComponentToCollection()
    {
        // Arrange
        var component = Substitute.For<ITuiComponent>();
        component.Name.Returns("TestComponent");

        // Act
        _componentManager.RegisterComponent(component);

        // Assert
        Assert.Single(_componentManager.Components);
        Assert.Equal(component, _componentManager.Components["TestComponent"]);
    }

    [Fact]
    public void RegisterComponent_WithNullComponent_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _componentManager.RegisterComponent(null!));
    }

    [Fact]
    public void RegisterComponent_WithDuplicateName_ShouldReplaceExistingComponent()
    {
        // Arrange
        var component1 = Substitute.For<ITuiComponent>();
        component1.Name.Returns("TestComponent");
        var component2 = Substitute.For<ITuiComponent>();
        component2.Name.Returns("TestComponent");

        // Act
        _componentManager.RegisterComponent(component1);
        _componentManager.RegisterComponent(component2);

        // Assert
        Assert.Single(_componentManager.Components);
        Assert.Equal(component2, _componentManager.Components["TestComponent"]);
    }

    [Fact]
    public void UnregisterComponent_WithExistingComponent_ShouldReturnTrueAndRemoveComponent()
    {
        // Arrange
        var component = Substitute.For<ITuiComponent>();
        component.Name.Returns("TestComponent");
        _componentManager.RegisterComponent(component);

        // Act
        var result = _componentManager.UnregisterComponent("TestComponent");

        // Assert
        Assert.True(result);
        Assert.Empty(_componentManager.Components);
    }

    [Fact]
    public void UnregisterComponent_WithNonExistentComponent_ShouldReturnFalse()
    {
        // Act
        var result = _componentManager.UnregisterComponent("NonExistentComponent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetComponent_WithExistingComponent_ShouldReturnComponent()
    {
        // Arrange
        var component = Substitute.For<ITuiComponent>();
        component.Name.Returns("TestComponent");
        _componentManager.RegisterComponent(component);

        // Act
        var result = _componentManager.GetComponent("TestComponent");

        // Assert
        Assert.Equal(component, result);
    }

    [Fact]
    public void GetComponent_WithNonExistentComponent_ShouldReturnNull()
    {
        // Act
        var result = _componentManager.GetComponent("NonExistentComponent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetComponentGeneric_WithExistingComponentOfCorrectType_ShouldReturnComponent()
    {
        // Arrange
        var component = Substitute.For<ITuiComponent>();
        component.Name.Returns("TestComponent");
        _componentManager.RegisterComponent(component);

        // Act
        var result = _componentManager.GetComponent<ITuiComponent>("TestComponent");

        // Assert
        Assert.Equal(component, result);
    }

    [Fact]
    public void SetComponentVisibility_WithExistingComponent_ShouldUpdateVisibility()
    {
        // Arrange
        var component = Substitute.For<ITuiComponent>();
        component.Name.Returns("TestComponent");
        component.IsVisible.Returns(false);
        _componentManager.RegisterComponent(component);

        // Act
        _componentManager.SetComponentVisibility("TestComponent", true);

        // Assert
        component.Received(1).IsVisible = true;
    }

    [Fact]
    public void RenderLayout_WithNoLayout_ShouldReturnText()
    {
        // Arrange
        var context = Substitute.For<IRenderContext>();

        // Act
        var result = _componentManager.RenderLayout(context);

        // Assert
        
        Assert.IsType<Text>(result);
    }

    [Fact]
    public void RenderLayout_WithValidLayout_ShouldCallLayoutCompose()
    {
        // Arrange
        var context = Substitute.For<IRenderContext>();
        var layout = Substitute.For<ITuiLayout>();
        var expectedRenderable = new Text("Test Layout");
        
        layout.ValidateComponents(Arg.Any<IReadOnlyDictionary<string, ITuiComponent>>()).Returns(true);
        layout.Compose(Arg.Any<IReadOnlyDictionary<string, ITuiComponent>>(), context).Returns(expectedRenderable);
        
        _componentManager.CurrentLayout = layout;

        // Act
        var result = _componentManager.RenderLayout(context);

        // Assert
        Assert.Equal(expectedRenderable, result);
        layout.Received(1).Compose(Arg.Any<IReadOnlyDictionary<string, ITuiComponent>>(), context);
    }

    [Fact]
    public async Task BroadcastInputAsync_WithVisibleComponents_ShouldCallHandleInputOnComponents()
    {
        // Arrange
        var context = Substitute.For<IRenderContext>();
        var inputEvent = new object();
        
        var component1 = Substitute.For<ITuiComponent>();
        component1.Name.Returns("Component1");
        component1.IsVisible.Returns(true);
        component1.HandleInputAsync(context, inputEvent).Returns(false);
        
        var component2 = Substitute.For<ITuiComponent>();
        component2.Name.Returns("Component2");
        component2.IsVisible.Returns(true);
        component2.HandleInputAsync(context, inputEvent).Returns(true);
        
        _componentManager.RegisterComponent(component1);
        _componentManager.RegisterComponent(component2);

        // Act
        var result = await _componentManager.BroadcastInputAsync(inputEvent, context);

        // Assert
        Assert.True(result);
        await component1.Received(1).HandleInputAsync(context, inputEvent);
        await component2.Received(1).HandleInputAsync(context, inputEvent);
    }

    [Fact]
    public async Task InitializeComponentsAsync_ShouldCallInitializeOnAllComponents()
    {
        // Arrange
        var context = Substitute.For<IRenderContext>();
        
        var component1 = Substitute.For<ITuiComponent>();
        component1.Name.Returns("Component1");
        
        var component2 = Substitute.For<ITuiComponent>();
        component2.Name.Returns("Component2");
        
        _componentManager.RegisterComponent(component1);
        _componentManager.RegisterComponent(component2);

        // Act
        await _componentManager.InitializeComponentsAsync(context);

        // Assert
        await component1.Received(1).InitializeAsync(context);
        await component2.Received(1).InitializeAsync(context);
    }

    [Fact]
    public async Task DisposeComponentsAsync_ShouldCallDisposeOnAllComponentsAndClearCollection()
    {
        // Arrange
        var component1 = Substitute.For<ITuiComponent>();
        component1.Name.Returns("Component1");
        
        var component2 = Substitute.For<ITuiComponent>();
        component2.Name.Returns("Component2");
        
        _componentManager.RegisterComponent(component1);
        _componentManager.RegisterComponent(component2);

        // Act
        await _componentManager.DisposeComponentsAsync();

        // Assert
        await component1.Received(1).DisposeAsync();
        await component2.Received(1).DisposeAsync();
        Assert.Empty(_componentManager.Components);
    }
}
