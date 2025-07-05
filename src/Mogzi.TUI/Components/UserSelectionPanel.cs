namespace Mogzi.TUI.Components;

/// <summary>
/// Displays user selection options for tool approvals and other choices.
/// Supports keyboard navigation and selection confirmation.
/// </summary>
public class UserSelectionPanel : BaseTuiComponent
{
    public override string Name => "UserSelectionPanel";

    protected override IRenderable RenderContent(IRenderContext context)
    {
        var inputContext = context.TuiContext.InputContext;

        if (inputContext.CompletionItems.Count == 0)
        {
            return new Text(string.Empty);
        }

        var selectionItems = inputContext.CompletionItems.Select((item, index) =>
        {
            var isSelected = index == inputContext.SelectedSuggestionIndex;
            var style = isSelected ? "[blue on white]" : "[dim]";
            var prefix = isSelected ? ">" : " ";

            return new Markup($"{style}{prefix} {item.Text,-12} {item.Description}[/]");
        }).ToArray();

        return new Panel(new Rows(selectionItems))
            .Header("Select an option")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green)
            .Padding(0, 0);
    }

    public override Task<bool> HandleInputAsync(IRenderContext context, object inputEvent)
    {
        // Input handling is delegated to the state manager and mediator
        // This component focuses on rendering
        return Task.FromResult(false);
    }

    public override Task InitializeAsync(IRenderContext context)
    {
        context.Logger.LogDebug("UserSelectionPanel initialized");
        return Task.CompletedTask;
    }

    public override Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
