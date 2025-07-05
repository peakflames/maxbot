namespace Mogzi.TUI.Components;

/// <summary>
/// Displays the welcome message and branding for the application.
/// Supports customizable welcome content and handles initial application state.
/// </summary>
public class WelcomePanel : BaseTuiComponent
{
    public override string Name => "WelcomePanel";

    protected override IRenderable RenderContent(IRenderContext context)
    {
        var contentItems = new List<IRenderable>
        {
            // Shadow effect style with multi-color letters - "Mogzi" with each letter different color
            new Markup("[bold blue]███╗   ███╗[/] [bold cyan] ██████╗ [/] [bold green] ██████╗ [/] [bold yellow]███████╗[/] [bold magenta]██╗[/]"),
            new Markup("[bold blue]████╗ ████║[/] [bold cyan]██╔═══██╗[/] [bold green]██╔════╝ [/] [bold yellow]╚══███╔╝[/] [bold magenta]██║[/]"),
            new Markup("[bold blue]██╔████╔██║[/] [bold cyan]██║   ██║[/] [bold green]██║  ███╗[/] [bold yellow]  ███╔╝ [/] [bold magenta]██║[/]"),
            new Markup("[bold blue]██║╚██╔╝██║[/] [bold cyan]██║   ██║[/] [bold green]██║   ██║[/] [bold yellow] ███╔╝  [/] [bold magenta]██║[/]"),
            new Markup("[bold blue]██║ ╚═╝ ██║[/] [bold cyan]╚██████╔╝[/] [bold green]╚██████╔╝[/] [bold yellow]███████╗[/] [bold magenta]██║[/]"),
            new Markup("[bold blue]╚═╝     ╚═╝[/] [bold cyan] ╚═════╝ [/] [bold green] ╚═════╝ [/] [bold yellow]╚══════╝[/] [bold magenta]╚═╝[/]"),
            new Text(""),
            new Markup("[bold cyan]◢◤◢◤◢◤ Now connected to your Multi-model Autonomous Assistant ◢◤◢◤◢◤[/]"),
            new Text(""),
            new Markup("[dim]Your AI-powered development assistant[/]"),
            new Text(""),
            new Markup("[grey69]Tips for getting started:[/]"),
            new Markup("[grey69]1. Ask questions, edit files, or run commands[/]"),
            new Markup("[grey69]2. Be specific for the best results[/]"),
            new Markup("[grey69]3. Use [/][magenta]/help[/][dim] for more information[/]"),
            new Text("")
        };

        return new Rows(contentItems);
    }
}
