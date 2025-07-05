namespace Mogzi.TUI.Components;

/// <summary>
/// Handles user input display and text editing functionality.
/// Supports autocomplete integration and input validation.
/// </summary>
public class InputPanel : BaseTuiComponent
{
    public override string Name => "InputPanel";

    protected override IRenderable RenderContent(IRenderContext context)
    {
        var prompt = "[blue]>[/] ";
        var cursor = "[blink]▋[/]";
        var currentInput = context.TuiContext.InputContext.CurrentInput;

        string content;
        if (string.IsNullOrEmpty(currentInput))
        {
            content = $"{prompt}{cursor}[dim]Type your message or /help[/]";
        }
        else
        {
            // Insert cursor at the correct position
            var beforeCursor = currentInput[..context.TuiContext.InputContext.CursorPosition];
            var afterCursor = currentInput[context.TuiContext.InputContext.CursorPosition..];
            content = $"{prompt}{beforeCursor}{cursor}{afterCursor}";
        }

        return new Panel(content)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey23)
            .Padding(1, 0, 1, 0)
            .Expand();
    }

    public override Task<bool> HandleInputAsync(IRenderContext context, object inputEvent)
    {
        // Input handling is delegated to the state manager and mediator
        // This component focuses on rendering
        return Task.FromResult(false);
    }

    public override Task InitializeAsync(IRenderContext context)
    {
        context.Logger.LogDebug("InputPanel initialized");
        return Task.CompletedTask;
    }

    public override Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
