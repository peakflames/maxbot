namespace Mogzi.TUI.Components;

/// <summary>
/// Displays autocomplete suggestions with selection highlighting.
/// Handles navigation and selection logic for different completion types.
/// </summary>
public class AutocompletePanel : ITuiComponent
{
    public string Name => "AutocompletePanel";
    public bool IsVisible { get; set; } = true;

    public IRenderable Render(IRenderContext context)
    {
        var inputContext = context.TuiContext.InputContext;

        if (!inputContext.ShowSuggestions || inputContext.Suggestions.Count == 0)
        {
            return new Text(string.Empty);
        }

        var suggestionItems = inputContext.Suggestions.Select((suggestion, index) =>
        {
            var isSelected = index == inputContext.SelectedSuggestionIndex;
            var style = isSelected ? "[blue on white]" : "[dim]";
            var prefix = isSelected ? ">" : " ";

            var description = context.TuiContext.SlashCommandProcessor.GetAllCommands()
                .GetValueOrDefault(suggestion, "");

            return new Markup($"{style}{prefix} {suggestion,-12} {description}[/]");
        }).ToArray();

        return new Panel(new Rows(suggestionItems))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue)
            .Padding(0, 0);
    }

    public Task<bool> HandleInputAsync(IRenderContext context, object inputEvent)
    {
        // Input handling is delegated to the state manager and mediator
        // This component focuses on rendering
        return Task.FromResult(false);
    }

    public Task InitializeAsync(IRenderContext context)
    {
        context.Logger.LogTrace("AutocompletePanel initialized");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
