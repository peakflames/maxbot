namespace Mogzi.TUI.Components;

/// <summary>
/// Displays tool execution results with clean, minimal visual feedback.
/// No heavy borders or nested panels - focuses on content clarity.
/// </summary>
public static class ToolExecutionDisplay
{
    /// <summary>
    /// Creates a clean visual display for tool execution with status indicator and optional diff.
    /// </summary>
    /// <param name="toolName">The name of the tool being executed</param>
    /// <param name="status">The execution status</param>
    /// <param name="description">Optional description of what the tool is doing</param>
    /// <param name="diff">Optional diff to display for file changes</param>
    /// <param name="result">Optional result content to display</param>
    /// <returns>A clean renderable component showing the tool execution</returns>
    public static IRenderable CreateToolDisplay(
        string toolName,
        ToolExecutionStatus status,
        string? description = null,
        UnifiedDiff? diff = null,
        string? result = null)
    {
        // Handle attempt_completion tool specially
        if (toolName.Equals("attempt_completion", StringComparison.OrdinalIgnoreCase))
        {
            return CreateCompletionDisplay(result ?? "Task completed successfully.");
        }

        var components = new List<IRenderable>();

        // Create clean tool status line
        var statusLine = CreateCleanToolStatus(toolName, status, description);
        components.Add(statusLine);

        // Add diff visualization if provided (with borders for replace_in_file tools)
        if (diff != null)
        {
            components.Add(DiffRenderer.RenderBorderedDiff(diff));
        }

        // Add result content if provided (for write_file operations)
        if (!string.IsNullOrWhiteSpace(result))
        {
            components.Add(CreateCleanResultDisplay(result, toolName, description));
        }

        return new Rows(components);
    }

    /// <summary>
    /// Creates a simple tool progress indicator for ongoing execution.
    /// </summary>
    /// <param name="toolName">The name of the tool being executed</param>
    /// <param name="description">Optional description of the current operation</param>
    /// <returns>A renderable progress indicator</returns>
    public static IRenderable CreateProgressIndicator(string toolName, string? description = null)
    {
        // Use unified animation utility for consistent timing
        var spinner = AnimationUtility.GetSpinnerFrame(4, 80);

        var text = !string.IsNullOrWhiteSpace(description) 
            ? $"{spinner} {toolName}: {description}"
            : $"{spinner} Executing {toolName}...";

        return new Markup($"[yellow]{text}[/]");
    }


    /// <summary>
    /// Creates a clean, compact tool status line matching.
    /// </summary>
    private static IRenderable CreateCleanToolStatus(string toolName, ToolExecutionStatus status, string? description)
    {
        var statusIcon = GetStatusIcon(status);
        var statusColor = GetStatusColor(status);

        // Create compact status line: "✓ ReadFile test_file.js"
        var statusText = !string.IsNullOrWhiteSpace(description)
            ? $"[{statusColor}]{statusIcon}[/] [bold]{toolName}[/] [dim]{description}[/]"
            : $"[{statusColor}]{statusIcon}[/] [bold]{toolName}[/]";

        // For completed tools, create a subtle visual distinction
        if (status == ToolExecutionStatus.Success)
        {
            // Use a simple panel without borders to create visual grouping
            return new Panel(new Markup(statusText))
                .Border(BoxBorder.None)
                .Padding(0, 0);
        }

        return new Markup(statusText);
    }

    /// <summary>
    /// Creates a clean result display for tool output, especially for write_file operations.
    /// </summary>
    private static IRenderable CreateCleanResultDisplay(string result, string toolName, string? description = null)
    {
        // For write_file operations, show the actual content written
        if (IsWriteFileTool(toolName))
        {
            return CreateBorderedFileContentDisplay(result, toolName, description);
        }

        // For other tools, show clean result without heavy borders
        if (result.StartsWith("<tool_response"))
        {
            return FormatCleanXmlResponse(result);
        }

        // Simple text display for other results
        return new Markup($"[dim]{result.EscapeMarkup()}[/]");
    }

    /// <summary>
    /// Creates a bordered display of file content for write operations with rounded borders.
    /// Shows the last ~50 lines for WriteFileTool as requested.
    /// </summary>
    private static IRenderable CreateBorderedFileContentDisplay(string content, string toolName, string? description = null)
    {
        var lines = content.Split('\n');
        var maxLines = 50;

        var components = new List<IRenderable>();

        // For WriteFileTool, show the last ~50 lines (or all if fewer)
        var startIndex = Math.Max(0, lines.Length - maxLines);
        var displayLines = lines.Skip(startIndex).ToArray();

        // Add truncation indicator if we're not showing the beginning
        if (startIndex > 0)
        {
            components.Add(new Markup($"[dim]... (showing last {displayLines.Length} of {lines.Length} lines)[/]"));
        }

        for (var i = 0; i < displayLines.Length; i++)
        {
            var actualLineNumber = startIndex + i + 1;
            var lineNumber = actualLineNumber.ToString().PadLeft(3);
            var lineContent = displayLines[i].EscapeMarkup();
            components.Add(new Markup($"[dim]{lineNumber}[/] {lineContent}"));
        }

        // Extract filename from description or tool name
        var fileName = ExtractFileNameFromDescription(description) ?? ExtractFileNameFromToolName(toolName) ?? "file";

        // Wrap content in a bordered panel with rounded borders
        return new Panel(new Rows(components))
            .Header($"[bold]{fileName}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey23)
            .Padding(1, 0);
    }

    /// <summary>
    /// Creates a clean display of file content for write operations.
    /// Shows the last ~50 lines for WriteFileTool as requested.
    /// </summary>
    private static IRenderable CreateFileContentDisplay(string content)
    {
        var lines = content.Split('\n');
        var maxLines = 50;

        var components = new List<IRenderable>();

        // For WriteFileTool, show the last ~50 lines (or all if fewer)
        var startIndex = Math.Max(0, lines.Length - maxLines);
        var displayLines = lines.Skip(startIndex).ToArray();

        // Add truncation indicator if we're not showing the beginning
        if (startIndex > 0)
        {
            components.Add(new Markup($"[dim]... (showing last {displayLines.Length} of {lines.Length} lines)[/]"));
        }

        for (var i = 0; i < displayLines.Length; i++)
        {
            var actualLineNumber = startIndex + i + 1;
            var lineNumber = actualLineNumber.ToString().PadLeft(3);
            var lineContent = displayLines[i].EscapeMarkup();
            components.Add(new Markup($"[dim]{lineNumber}[/] {lineContent}"));
        }

        return new Rows(components);
    }

    /// <summary>
    /// Formats XML tool responses without heavy borders.
    /// </summary>
    private static IRenderable FormatCleanXmlResponse(string xmlResponse)
    {
        try
        {
            var lines = new List<string>();

            if (xmlResponse.Contains("<notes>"))
            {
                var notesStart = xmlResponse.IndexOf("<notes>") + 7;
                var notesEnd = xmlResponse.IndexOf("</notes>");
                if (notesEnd > notesStart)
                {
                    var notes = xmlResponse[notesStart..notesEnd].Trim();
                    lines.AddRange(notes.Split('\n').Select(line => $"[dim]{line.Trim().EscapeMarkup()}[/]"));
                }
            }

            if (xmlResponse.Contains("status=\"SUCCESS\""))
            {
                lines.Add("[green]✓ Operation completed successfully[/]");
            }
            else if (xmlResponse.Contains("status=\"FAILED\""))
            {
                lines.Add("[red]✗ Operation failed[/]");

                if (xmlResponse.Contains("<error>"))
                {
                    var errorStart = xmlResponse.IndexOf("<error>") + 7;
                    var errorEnd = xmlResponse.IndexOf("</error>");
                    if (errorEnd > errorStart)
                    {
                        var error = xmlResponse[errorStart..errorEnd].Trim();
                        lines.Add($"[red]Error: {error.EscapeMarkup()}[/]");
                    }
                }
            }

            return lines.Count > 0
                ? new Rows(lines.Select(line => new Markup(line)))
                : new Markup($"[dim]{xmlResponse.EscapeMarkup()}[/]");
        }
        catch
        {
            return new Markup($"[dim]{xmlResponse.EscapeMarkup()}[/]");
        }
    }

    private static bool IsWriteFileTool(string toolName)
    {
        var writeTools = new[] { "write_file", "writefile", "write_to_file", "WriteFile" };
        return writeTools.Contains(toolName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts filename from description text for display purposes.
    /// </summary>
    private static string? ExtractFileNameFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        // Look for common filename patterns in the description
        // e.g., "test_file.js", "src/components/App.tsx", etc.
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            // Check if word looks like a filename (has extension)
            if (word.Contains('.') && !word.StartsWith('.') && word.Length > 3)
            {
                // Extract just the filename part if it's a path
                return Path.GetFileName(word);
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts filename from tool name or description for display purposes.
    /// </summary>
    private static string? ExtractFileNameFromToolName(string toolName)
    {
        // For now, return a generic name since we don't have the actual filename
        // In the future, this could be enhanced to extract from tool arguments
        return "file";
    }

    private static string GetStatusIcon(ToolExecutionStatus status)
    {
        return status switch
        {
            ToolExecutionStatus.Executing => "⏳",
            ToolExecutionStatus.Success => "✓",
            ToolExecutionStatus.Failed => "✗",
            ToolExecutionStatus.Confirming => "?",
            _ => "•"
        };
    }

    private static string GetStatusColor(ToolExecutionStatus status)
    {
        return status switch
        {
            ToolExecutionStatus.Executing => "yellow",
            ToolExecutionStatus.Success => "green",
            ToolExecutionStatus.Failed => "red",
            ToolExecutionStatus.Confirming => "blue",
            _ => "white"
        };
    }

    /// <summary>
    /// Creates a prominent display for task completion messages.
    /// </summary>
    private static IRenderable CreateCompletionDisplay(string completionMessage)
    {
        var components = new List<IRenderable>
        {
            new Markup(""),
            new Markup(":check_mark_button: [bold green]Task Completed[/]"),
            new Markup(""),
            new Markup($"[green]{completionMessage.EscapeMarkup()}[/]"),
            new Markup("")
        };

        return new Panel(new Rows(components))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green)
            .Padding(1, 0);
    }
}
