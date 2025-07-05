namespace Mogzi.TUI.Components;

/// <summary>
/// Displays footer information including status, shortcuts, and help text.
/// Provides contextual information based on current application state.
/// </summary>
public class FooterPanel : BaseTuiComponent
{
    public override string Name => "FooterPanel";

    protected override IRenderable RenderContent(IRenderContext context)
    {
        var currentDir = context.RenderingUtilities.FormatDisplayPath(
            context.TuiContext.WorkingDirectoryProvider.GetCurrentDirectory());
        var modelInfo = context.RenderingUtilities.FormatModelInfo(context.TuiContext.AppService);
        var tokenInfo = context.RenderingUtilities.FormatTokenUsage(
            context.TuiContext.AppService, 
            context.TuiContext.HistoryManager.GetCurrentChatHistory());

        var content = $"[skyblue2]{currentDir}[/]  [rosybrown]{modelInfo}[/] [dim]({tokenInfo})[/]";

        return new Panel(new Markup(content))
            .NoBorder();
    }

    public override Task<bool> HandleInputAsync(IRenderContext context, object inputEvent)
    {
        // Footer panel doesn't handle input events
        return Task.FromResult(false);
    }

    public override Task InitializeAsync(IRenderContext context)
    {
        context.Logger.LogDebug("FooterPanel initialized");
        return Task.CompletedTask;
    }

    public override Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
