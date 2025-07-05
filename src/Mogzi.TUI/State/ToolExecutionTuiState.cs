namespace Mogzi.TUI.State;

#pragma warning disable IDE0010 // Add missing cases

/// <summary>
/// Handles the tool execution state of the TUI application.
/// Manages tool execution display with progress indicators and cancellation support.
/// </summary>
public class ToolExecutionTuiState : ITuiState
{
    public string Name => "ToolExecution";

    public IRenderable RenderDynamicContent(ITuiContext context)
    {
        // DEAD CODE: This method is NEVER CALLED by the real application!
        // The real app uses ProgressPanel component for spinner rendering.
        // This exists only for test infrastructure that should be updated.
        
        var progressText = string.IsNullOrWhiteSpace(context.ToolProgress)
            ? (!string.IsNullOrWhiteSpace(context.CurrentToolName)
                ? $"Executing {context.CurrentToolName}..."
                : "Executing tool...")
            : context.ToolProgress;

        var toolExecutionComponent = new Panel(new Markup($"⠋ [dim]{progressText} Press Esc to cancel[/]"))
            .NoBorder();

        return new Rows(new Text(""), toolExecutionComponent, new Text(""), CreateFlexFooterComponent(context));
    }

    public async Task HandleKeyPressAsync(ITuiContext context, KeyPressEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        try
        {
            switch (e.Key)
            {
                case ConsoleKey.Escape:
                    await InterruptToolExecution(context);
                    e.Handled = true;
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error handling key press in ToolExecutionTuiState: {Key}", e.Key);
        }
    }

    public Task HandleCharacterTypedAsync(ITuiContext context, CharacterTypedEventArgs e)
    {
        // No character input handling in tool execution state
        return Task.CompletedTask;
    }

    public Task OnEnterAsync(ITuiContext context, ITuiState? previousState)
    {
        context.Logger.LogDebug("Entering ToolExecutionTuiState from {PreviousState}", previousState?.Name ?? "none");
        return Task.CompletedTask;
    }

    public Task OnExitAsync(ITuiContext context, ITuiState? nextState)
    {
        context.Logger.LogDebug("Exiting ToolExecutionTuiState to {NextState}", nextState?.Name ?? "none");
        return Task.CompletedTask;
    }

    private async Task InterruptToolExecution(ITuiContext context)
    {
        try
        {
            // Cancel the AI operation (which includes tool execution)
            context.AiOperationCts?.Cancel();

            // Clear any progress indicators
            context.ToolProgress = string.Empty;
            context.CurrentToolName = string.Empty;

            // Display interruption message without assistant prefix
            context.ScrollbackTerminal.WriteStatic(new Markup("⚠ Tool execution cancelled."));
            context.ScrollbackTerminal.WriteStatic(new Markup(""));

            // Add to history as assistant message for context
            var interruptMessage = new ChatMessage(ChatRole.Assistant, "⚠ Tool execution cancelled.");
            context.HistoryManager.AddAssistantMessage(interruptMessage);

            context.Logger?.LogInformation("Tool execution interrupted by user (Escape key)");

            // Return to input state
            await context.RequestStateTransitionAsync(ChatState.Input);
        }
        catch (Exception ex)
        {
            context.Logger?.LogError(ex, "Error interrupting tool execution");
        }
    }

    private IRenderable CreateFlexFooterComponent(ITuiContext context)
    {
        var currentDir = GetDisplayPath(context.WorkingDirectoryProvider.GetCurrentDirectory());
        var modelInfo = GetModelDisplayInfo(context);
        var tokenInfo = GetTokenUsageInfo(context);
        var content = $"[skyblue2]{currentDir}[/]  [rosybrown]{modelInfo}[/] [dim]({tokenInfo})[/]";
        return new Panel(new Markup(content))
            .NoBorder();
    }

    private string GetTokenUsageInfo(ITuiContext context)
    {
        try
        {
            var chatHistory = context.HistoryManager.GetCurrentChatHistory();
            var tokenCount = context.AppService.CalculateTokenMetrics(chatHistory);

            // Estimate context window size based on model (this could be made configurable)
            var contextWindowSize = EstimateContextWindowSize(context);
            var percentageUsed = Math.Min(100.0, tokenCount * 100.0 / contextWindowSize);
            var percentageLeft = 100.0 - percentageUsed;

            return $"{tokenCount:N0} tokens, {percentageLeft:F2}% context left";
        }
        catch (Exception ex)
        {
            context.Logger?.LogWarning(ex, "Error calculating token usage");
            return "token calculation unavailable";
        }
    }

    private int EstimateContextWindowSize(ITuiContext context)
    {
        try
        {
            var modelId = context.AppService.ChatClient.ActiveProfile.ModelId.ToLowerInvariant();

            // Common model context window sizes
            return modelId switch
            {
                var m when m.Contains("gpt-4.1") => 1047576, // GPT-4.1 models
                var m when m.Contains("gpt-4") => 128000,  // GPT-4 Turbo models
                var m when m.Contains("gpt-3.5-") => 16385, // GPT-3.5 Turbo models
                var m when m.Contains("o3") => 200000, // o3 models
                var m when m.Contains("o4") => 200000, // o4 models
                var m when m.Contains("gemini-2.5") => 1048576, // Gemini 2.5 Flash models
                var m when m.Contains("gemini-1.5") => 1048576, // Gemini 1.5 Pro models
                var m when m.Contains("claude") => 200000, // Claude models
                _ => 128000 // Default fallback
            };
        }
        catch (Exception ex)
        {
            context.Logger?.LogWarning(ex, "Error estimating context window size");
            return 128000; // Safe default
        }
    }

    private string GetModelDisplayInfo(ITuiContext context)
    {
        try
        {
            var chatClient = context.AppService.ChatClient;
            var provider = chatClient.ActiveApiProvider.Name;
            var model = chatClient.ActiveProfile.ModelId;

            // Format like "provider/model" or just "model" if provider is empty
            if (!string.IsNullOrEmpty(provider))
            {
                return $"{provider}:{model}";
            }
            return model;
        }
        catch (Exception ex)
        {
            context.Logger?.LogWarning(ex, "Error getting model display info");
            return "unknown-model";
        }
    }

    private string GetDisplayPath(string fullPath)
    {
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (fullPath.StartsWith(homeDir))
            {
                return "~" + fullPath[homeDir.Length..].Replace('\\', '/');
            }
            return fullPath.Replace('\\', '/');
        }
        catch
        {
            return fullPath;
        }
    }
}

#pragma warning restore IDE0010 // Add missing cases
