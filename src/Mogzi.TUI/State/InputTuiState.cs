namespace Mogzi.TUI.State;

#pragma warning disable IDE0010 // Add missing cases
#pragma warning disable IDE0045 // Convert to conditional expression

/// <summary>
/// Handles the input state of the TUI application.
/// Manages normal input, autocomplete, and user selection modes.
/// </summary>
public class InputTuiState : ITuiState
{
    public string Name => "Input";

    public IRenderable RenderDynamicContent(ITuiContext context)
    {
        var bottomComponent = context.InputContext.State switch
        {
            InputState.UserSelection => CreateInputWithUserSelection(context),
            InputState.Autocomplete when context.InputContext.ShowSuggestions => CreateInputWithAutocomplete(context),
            _ => CreateFlexInputComponent(context)
        };

        return new Rows(new Text(""), bottomComponent, new Text(""), CreateFlexFooterComponent(context));
    }

    public async Task HandleKeyPressAsync(ITuiContext context, KeyPressEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        try
        {
            // Handle autocomplete navigation first
            if (context.InputContext.State == InputState.Autocomplete && context.InputContext.ShowSuggestions)
            {
                switch (e.Key)
                {
                    case ConsoleKey.UpArrow:
                        NavigateAutocomplete(context, up: true);
                        e.Handled = true;
                        return;

                    case ConsoleKey.DownArrow:
                        NavigateAutocomplete(context, up: false);
                        e.Handled = true;
                        return;

                    case ConsoleKey.Tab:
                    case ConsoleKey.Enter:
                        AcceptAutocompleteSuggestion(context);
                        e.Handled = true;
                        return;

                    case ConsoleKey.Escape:
                        CancelAutocomplete(context);
                        e.Handled = true;
                        return;
                }
            }
            else if (context.InputContext.State == InputState.UserSelection)
            {
                switch (e.Key)
                {
                    case ConsoleKey.UpArrow:
                        NavigateUserSelection(context, up: true);
                        e.Handled = true;
                        return;

                    case ConsoleKey.DownArrow:
                        NavigateUserSelection(context, up: false);
                        e.Handled = true;
                        return;

                    case ConsoleKey.Enter:
                        await context.UserSelectionManager.AcceptSelectionAsync();
                        e.Handled = true;
                        return;

                    case ConsoleKey.Escape:
                        context.UserSelectionManager.Deactivate();
                        e.Handled = true;
                        return;
                }
            }

            switch (e.Key)
            {
                case ConsoleKey.Enter:
                    await SubmitCurrentInput(context);
                    e.Handled = true;
                    break;

                case ConsoleKey.UpArrow:
                    NavigateCommandHistory(context, up: true);
                    e.Handled = true;
                    break;

                case ConsoleKey.DownArrow:
                    NavigateCommandHistory(context, up: false);
                    e.Handled = true;
                    break;

                case ConsoleKey.LeftArrow:
                    MoveCursorLeft(context);
                    e.Handled = true;
                    break;

                case ConsoleKey.RightArrow:
                    MoveCursorRight(context);
                    e.Handled = true;
                    break;

                case ConsoleKey.Home:
                    MoveCursorToStart(context);
                    e.Handled = true;
                    break;

                case ConsoleKey.End:
                    MoveCursorToEnd(context);
                    e.Handled = true;
                    break;

                case ConsoleKey.Backspace:
                    DeleteCharacterBefore(context);
                    e.Handled = true;
                    break;

                case ConsoleKey.Delete:
                    DeleteCharacterAfter(context);
                    e.Handled = true;
                    break;

                case ConsoleKey.Escape:
                    ClearCurrentInput(context);
                    e.Handled = true;
                    break;
            }
        }
        catch
        {
            context.InputContext.ClearAutocomplete();
        }
    }

    public async Task HandleCharacterTypedAsync(ITuiContext context, CharacterTypedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        try
        {
            InsertCharacter(context, e.Character);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error handling character typed in InputTuiState: {Character}", e.Character);
        }

        await Task.CompletedTask;
    }

    public Task OnEnterAsync(ITuiContext context, ITuiState? previousState)
    {
        context.Logger.LogDebug("Entering InputTuiState from {PreviousState}", previousState?.Name ?? "none");
        return Task.CompletedTask;
    }

    public Task OnExitAsync(ITuiContext context, ITuiState? nextState)
    {
        context.Logger.LogDebug("Exiting InputTuiState to {NextState}", nextState?.Name ?? "none");
        return Task.CompletedTask;
    }

    private IRenderable CreateFlexInputComponent(ITuiContext context)
    {
        var prompt = "[blue]>[/] ";
        var cursor = "[blink]▋[/]";
        var currentInput = context.InputContext.CurrentInput;

        string content;
        if (string.IsNullOrEmpty(currentInput))
        {
            content = $"{prompt}{cursor}[dim]Type your message or /help[/]";
        }
        else
        {
            // Insert cursor at the correct position
            var beforeCursor = currentInput[..context.InputContext.CursorPosition];
            var afterCursor = currentInput[context.InputContext.CursorPosition..];
            content = $"{prompt}{beforeCursor}{cursor}{afterCursor}";
        }

        return new Panel(content)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey23)
            .Padding(1, 0, 1, 0)
            .Expand();
    }

    private IRenderable CreateInputWithAutocomplete(ITuiContext context)
    {
        var inputPanel = CreateFlexInputComponent(context);

        if (!context.InputContext.ShowSuggestions || context.InputContext.Suggestions.Count == 0)
        {
            return inputPanel;
        }

        var suggestionItems = context.InputContext.Suggestions.Select((suggestion, index) =>
        {
            var isSelected = index == context.InputContext.SelectedSuggestionIndex;
            var style = isSelected ? "[blue on white]" : "[dim]";
            var prefix = isSelected ? ">" : " ";

            var description = context.SlashCommandProcessor.GetAllCommands()
                .GetValueOrDefault(suggestion, "");

            return new Markup($"{style}{prefix} {suggestion,-12} {description}[/]");
        }).ToArray();

        var suggestionsPanel = new Panel(new Rows(suggestionItems))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue)
            .Padding(0, 0);

        return new Rows(inputPanel, suggestionsPanel);
    }

    private IRenderable CreateInputWithUserSelection(ITuiContext context)
    {
        var inputPanel = CreateFlexInputComponent(context);

        if (context.InputContext.CompletionItems.Count == 0)
        {
            return inputPanel;
        }

        var selectionItems = context.InputContext.CompletionItems.Select((item, index) =>
        {
            var isSelected = index == context.InputContext.SelectedSuggestionIndex;
            var style = isSelected ? "[blue on white]" : "[dim]";
            var prefix = isSelected ? ">" : " ";

            return new Markup($"{style}{prefix} {item.Text,-12} {item.Description}[/]");
        }).ToArray();

        var selectionPanel = new Panel(new Rows(selectionItems))
            .Header("Select an option")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green)
            .Padding(0, 0);

        return new Rows(inputPanel, selectionPanel);
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

    private void NavigateCommandHistory(ITuiContext context, bool up)
    {
        if (context.CommandHistory.Count == 0)
        {
            return;
        }

        if (up)
        {
            if (context.CommandHistoryIndex == -1)
            {
                context.CommandHistoryIndex = context.CommandHistory.Count - 1;
                context.InputContext.CurrentInput = context.CommandHistory[context.CommandHistoryIndex];
            }
            else if (context.CommandHistoryIndex > 0)
            {
                context.CommandHistoryIndex--;
                context.InputContext.CurrentInput = context.CommandHistory[context.CommandHistoryIndex];
            }
        }
        else
        {
            if (context.CommandHistoryIndex >= 0 && context.CommandHistoryIndex < context.CommandHistory.Count - 1)
            {
                context.CommandHistoryIndex++;
                context.InputContext.CurrentInput = context.CommandHistory[context.CommandHistoryIndex];
            }
            else if (context.CommandHistoryIndex == context.CommandHistory.Count - 1)
            {
                context.CommandHistoryIndex = -1;
                context.InputContext.CurrentInput = string.Empty;
            }
        }

        context.InputContext.CursorPosition = context.InputContext.CurrentInput.Length;
        UpdateAutocompleteState(context);
    }

    private async Task SubmitCurrentInput(ITuiContext context)
    {
        if (string.IsNullOrWhiteSpace(context.InputContext.CurrentInput))
        {
            return;
        }

        var inputToSubmit = context.InputContext.CurrentInput;
        AddToCommandHistory(context, inputToSubmit);
        ClearCurrentInput(context);

        await ProcessUserInput(context, inputToSubmit);
    }

    private void AddToCommandHistory(ITuiContext context, string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        if (context.CommandHistory.Contains(command))
        {
            return;
        }

        context.CommandHistory.Add(command);

        if (context.CommandHistory.Count > 100)
        {
            context.CommandHistory.RemoveAt(0);
        }

        context.CommandHistoryIndex = -1;
    }

    private async Task ProcessUserInput(ITuiContext context, string input)
    {
        try
        {
            // Get current environment context
            var envPrompt = EnvSystemPrompt.GetEnvPrompt(
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                context.AppService.ChatClient.OperatingSystem.ToString(),
                context.AppService.ChatClient.DefaultShell,
                context.AppService.ChatClient.Username,
                context.AppService.ChatClient.Hostname,
                context.WorkingDirectoryProvider.GetCurrentDirectory(),
                "chat", // mode - could be made configurable
                context.AppService.ChatClient.Config.ToolApprovals
            );

            context.Logger?.LogDebug("Generated environment prompt: {EnvPrompt}", envPrompt);

            // Create user message with environment context appended (for AI processing)
            var fullUserMessage = Mogzi.Utils.MessageUtils.AppendSystemEnvironment(input, envPrompt);
            var userMessage = new ChatMessage(ChatRole.User, fullUserMessage);
            context.HistoryManager.AddUserMessage(userMessage);

            context.Logger?.LogDebug("Full user message (with env context) length: {Length}", fullUserMessage.Length);
            context.Logger?.LogDebug("Original user input: {Input}", input);

            // Add spacing before user message
            context.ScrollbackTerminal.WriteStatic(new Markup(""));

            // Display only the original user input (stripped of env context)
            var displayMessage = new ChatMessage(ChatRole.User, input);
            context.ScrollbackTerminal.WriteStatic(RenderMessage(displayMessage));

            if (context.SlashCommandProcessor.TryProcessCommand(input, out var commandOutput))
            {
                if (context.InputContext.State == InputState.UserSelection)
                {
                    // Command is interactive, so we don't process it as a chat message.
                    return;
                }

                if (!string.IsNullOrEmpty(commandOutput))
                {
                    var commandMessage = new ChatMessage(ChatRole.Assistant, commandOutput);
                    context.HistoryManager.AddAssistantMessage(commandMessage);
                    context.ScrollbackTerminal.WriteStatic(RenderMessage(commandMessage));
                }
                return;
            }

            // Transition to thinking state and start AI processing
            await context.RequestStateTransitionAsync(ChatState.Thinking);
        }
        catch (Exception ex)
        {
            context.Logger?.LogError(ex, "Error processing user input");
            var errorMessage = new ChatMessage(ChatRole.Assistant, $"Error processing input: {ex.Message}");
            context.HistoryManager.AddAssistantMessage(errorMessage);
            context.ScrollbackTerminal.WriteStatic(RenderMessage(errorMessage));
        }
    }

    private IRenderable RenderMessage(ChatMessage message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            return new Text(string.Empty);
        }

        var messageType = GetMessageType(message);
        var prefix = messageType switch
        {
            MessageType.User => "[dim]>[/] ",
            MessageType.Assistant => "✦ ",
            _ => ""
        };
        var color = messageType switch
        {
            MessageType.User => "dim",
            MessageType.Assistant => "skyblue1",
            _ => "white"
        };

        // Strip system environment context from user messages for display
        var displayText = message.Role == ChatRole.User
            ? Mogzi.Utils.MessageUtils.StripSystemEnvironment(message.Text)
            : message.Text;

        return new Markup($"[{color}]{prefix}{displayText}[/]");
    }

    private MessageType GetMessageType(ChatMessage message)
    {
        if (message.Role == ChatRole.User)
        {
            return MessageType.User;
        }
        else if (message.Role == ChatRole.Assistant)
        {
            return MessageType.Assistant;
        }
        else
        {
            return MessageType.System;
        }
    }

    private void InsertCharacter(ITuiContext context, char character)
    {
        context.InputContext.CursorPosition = Math.Max(0, Math.Min(context.InputContext.CursorPosition, context.InputContext.CurrentInput.Length));
        context.InputContext.CurrentInput = context.InputContext.CurrentInput.Insert(context.InputContext.CursorPosition, character.ToString());
        context.InputContext.CursorPosition++;
        context.CommandHistoryIndex = -1;
        UpdateAutocompleteState(context);
    }

    private void DeleteCharacterBefore(ITuiContext context)
    {
        if (context.InputContext.CurrentInput.Length == 0)
        {
            return;
        }

        context.InputContext.CursorPosition = Math.Max(0, Math.Min(context.InputContext.CursorPosition, context.InputContext.CurrentInput.Length));

        if (context.InputContext.CursorPosition > 0)
        {
            context.InputContext.CurrentInput = context.InputContext.CurrentInput.Remove(context.InputContext.CursorPosition - 1, 1);
            context.InputContext.CursorPosition--;
        }

        context.CommandHistoryIndex = -1;
        UpdateAutocompleteState(context);
    }

    private void DeleteCharacterAfter(ITuiContext context)
    {
        if (context.InputContext.CurrentInput.Length == 0)
        {
            return;
        }

        context.InputContext.CursorPosition = Math.Max(0, Math.Min(context.InputContext.CursorPosition, context.InputContext.CurrentInput.Length));

        if (context.InputContext.CursorPosition < context.InputContext.CurrentInput.Length)
        {
            context.InputContext.CurrentInput = context.InputContext.CurrentInput.Remove(context.InputContext.CursorPosition, 1);
        }

        context.CommandHistoryIndex = -1;
        UpdateAutocompleteState(context);
    }

    private void MoveCursorLeft(ITuiContext context)
    {
        if (context.InputContext.CursorPosition > 0)
        {
            context.InputContext.CursorPosition--;
            UpdateAutocompleteState(context);
        }
    }

    private void MoveCursorRight(ITuiContext context)
    {
        if (context.InputContext.CursorPosition < context.InputContext.CurrentInput.Length)
        {
            context.InputContext.CursorPosition++;
            UpdateAutocompleteState(context);
        }
    }

    private void MoveCursorToStart(ITuiContext context)
    {
        context.InputContext.CursorPosition = 0;
        UpdateAutocompleteState(context);
    }

    private void MoveCursorToEnd(ITuiContext context)
    {
        context.InputContext.CursorPosition = context.InputContext.CurrentInput.Length;
        UpdateAutocompleteState(context);
    }

    private void ClearCurrentInput(ITuiContext context)
    {
        context.InputContext.Clear();
        context.CommandHistoryIndex = -1;
    }

    private async void UpdateAutocompleteState(ITuiContext context)
    {
        try
        {
            // Detect which provider should be triggered
            var provider = context.AutocompleteManager.DetectTrigger(context.InputContext.CurrentInput, context.InputContext.CursorPosition);

            if (provider != null)
            {
                context.InputContext.ActiveProvider = provider;
                await context.AutocompleteManager.UpdateSuggestionsAsync(context.InputContext);
            }
            else
            {
                context.InputContext.ClearAutocomplete();
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error updating autocomplete state");
            context.InputContext.ClearAutocomplete();
        }
    }

    private void NavigateAutocomplete(ITuiContext context, bool up)
    {
        if (!context.InputContext.ShowSuggestions || context.InputContext.Suggestions.Count == 0)
        {
            return;
        }

        if (up)
        {
            context.InputContext.SelectedSuggestionIndex =
                (context.InputContext.SelectedSuggestionIndex - 1 + context.InputContext.Suggestions.Count)
                % context.InputContext.Suggestions.Count;
        }
        else
        {
            context.InputContext.SelectedSuggestionIndex =
                (context.InputContext.SelectedSuggestionIndex + 1)
                % context.InputContext.Suggestions.Count;
        }
    }

    private void AcceptAutocompleteSuggestion(ITuiContext context)
    {
        context.AutocompleteManager.AcceptSuggestion(context.InputContext);
    }

    private void CancelAutocomplete(ITuiContext context)
    {
        context.InputContext.ClearAutocomplete();
    }

    private void NavigateUserSelection(ITuiContext context, bool up)
    {
        if (context.InputContext.CompletionItems.Count == 0)
        {
            return;
        }


        if (up)
        {
            context.InputContext.SelectedSuggestionIndex =
                (context.InputContext.SelectedSuggestionIndex - 1 + context.InputContext.CompletionItems.Count)
                % context.InputContext.CompletionItems.Count;
        }
        else
        {
            context.InputContext.SelectedSuggestionIndex =
                (context.InputContext.SelectedSuggestionIndex + 1)
                % context.InputContext.CompletionItems.Count;
        }
    }
}

#pragma warning restore IDE0010 // Add missing cases
#pragma warning restore IDE0045 // Convert to conditional expression
