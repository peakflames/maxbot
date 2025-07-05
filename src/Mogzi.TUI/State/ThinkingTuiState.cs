namespace Mogzi.TUI.State;

#pragma warning disable IDE0010 // Add missing cases

/// <summary>
/// Handles the thinking state of the TUI application.
/// Manages AI processing state with animation and cancellation support.
/// </summary>
public class ThinkingTuiState : ITuiState
{
    public string Name => "Thinking";

    public IRenderable RenderDynamicContent(ITuiContext context)
    {
        // DEAD CODE: This method is NEVER CALLED by the real application!
        // The real app uses ProgressPanel component for spinner rendering.
        // This exists only for test infrastructure that should be updated.
        
        var thinkingComponent = new Panel(new Markup("⠋ [dim]Thinking... Press Esc to cancel[/]"))
            .NoBorder();

        return new Rows(new Text(""), thinkingComponent, new Text(""), CreateFlexFooterComponent(context));
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
                    await InterruptAiOperation(context);
                    e.Handled = true;
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error handling key press in ThinkingTuiState: {Key}", e.Key);
        }
    }

    public Task HandleCharacterTypedAsync(ITuiContext context, CharacterTypedEventArgs e)
    {
        // No character input handling in thinking state
        return Task.CompletedTask;
    }

    public async Task OnEnterAsync(ITuiContext context, ITuiState? previousState)
    {
        context.Logger.LogDebug("Entering ThinkingTuiState from {PreviousState}", previousState?.Name ?? "none");

        // Start AI processing when entering thinking state
        await StartAiProcessing(context);
    }

    public Task OnExitAsync(ITuiContext context, ITuiState? nextState)
    {
        context.Logger.LogDebug("Exiting ThinkingTuiState to {NextState}", nextState?.Name ?? "none");
        return Task.CompletedTask;
    }

    private async Task StartAiProcessing(ITuiContext context)
    {
        try
        {
            // Create new cancellation token for this AI operation
            context.AiOperationCts?.Dispose();
            context.AiOperationCts = new CancellationTokenSource();
            context.AiOperationStartTime = DateTime.Now;

            var chatHistory = context.HistoryManager.GetCurrentChatHistory();

            // Check if there's any chat history to process
            if (chatHistory.Count == 0)
            {
                context.Logger?.LogDebug("No chat history to process, returning to Input state");
                await context.RequestStateTransitionAsync(ChatState.Input);
                return;
            }

            var responseStream = context.AppService.ProcessChatMessageAsync(chatHistory, context.AiOperationCts.Token);

            var assistantMessage = new ChatMessage(ChatRole.Assistant, "");
            context.HistoryManager.AddAssistantMessage(assistantMessage);
            context.ScrollbackTerminal.WriteStatic(new Markup(""));
            context.ScrollbackTerminal.WriteStatic(RenderMessage(assistantMessage), isUpdatable: true);

            var streamCompleted = false;

            await foreach (var responseUpdate in responseStream)
            {
                if (!string.IsNullOrEmpty(responseUpdate.Text))
                {
                    var newText = assistantMessage.Text + responseUpdate.Text;
                    context.Logger?.LogInformation($"ChatMsg[Assistant, '{newText}'");
                    assistantMessage = new ChatMessage(ChatRole.Assistant, newText);
                    context.HistoryManager.UpdateLastMessage(assistantMessage);
                    context.ScrollbackTerminal.WriteStatic(RenderMessage(assistantMessage), isUpdatable: true);
                }

                if (IsToolExecutionUpdate(responseUpdate))
                {
                    // Transition to tool execution state
                    await context.RequestStateTransitionAsync(ChatState.ToolExecution);

                    // Extract tool name first
                    ExtractToolNameFromUpdate(context, responseUpdate);

                    // Set progress text based on available information
                    if (!string.IsNullOrWhiteSpace(responseUpdate.Text))
                    {
                        context.ToolProgress = responseUpdate.Text;
                    }
                    else if (!string.IsNullOrWhiteSpace(context.CurrentToolName))
                    {
                        context.ToolProgress = $"Executing {context.CurrentToolName}...";
                        context.ScrollbackTerminal.WriteStatic(new Markup($"[green]•[/] [dim]{context.ToolProgress}[/]"));
                    }

                    context.Logger?.LogInformation($"ChatMsg[Tool, '{context.ToolProgress}'");

                    // Handle tool result display
                    await HandleToolExecutionResult(context, responseUpdate);
                }
            }

            // Mark stream as completed
            streamCompleted = true;
            context.Logger?.LogDebug("RACE_CONDITION_DEBUG: Stream processing completed, streamCompleted = {StreamCompleted}", streamCompleted);

            // Only add final spacing and transition - NO duplicate message writing
            if (streamCompleted)
            {
                context.ScrollbackTerminal.WriteStatic(new Markup(""));
                context.Logger?.LogDebug("RACE_CONDITION_DEBUG: Requesting transition to Input state");
                await context.RequestStateTransitionAsync(ChatState.Input);
                context.Logger?.LogDebug("RACE_CONDITION_DEBUG: State transition request completed");
            }
        }
        catch (OperationCanceledException) when (context.AiOperationCts?.Token.IsCancellationRequested == true)
        {
            // AI operation was cancelled by user - this is handled by InterruptAiOperation()
            context.Logger?.LogDebug("AI operation was cancelled by user");
        }
        catch (Exception ex)
        {
            context.Logger?.LogError(ex, "Error during AI processing");
            var errorMessage = new ChatMessage(ChatRole.Assistant, $"Error processing input: {ex.Message}");
            context.HistoryManager.AddAssistantMessage(errorMessage);
            context.ScrollbackTerminal.WriteStatic(RenderMessage(errorMessage));

            // Return to input state on error
            await context.RequestStateTransitionAsync(ChatState.Input);
        }
        finally
        {
            context.ToolProgress = string.Empty;
            context.CurrentToolName = string.Empty;
        }
    }

    private async Task InterruptAiOperation(ITuiContext context)
    {
        try
        {
            // Cancel the AI operation
            context.AiOperationCts?.Cancel();

            // Clear any progress indicators
            context.ToolProgress = string.Empty;
            context.CurrentToolName = string.Empty;

            // Display interruption message without assistant prefix
            context.ScrollbackTerminal.WriteStatic(new Markup("⚠ Request cancelled."));
            context.ScrollbackTerminal.WriteStatic(new Markup(""));

            // Add to history as assistant message for context
            var interruptMessage = new ChatMessage(ChatRole.Assistant, "⚠ Request cancelled.");
            context.HistoryManager.AddAssistantMessage(interruptMessage);

            context.Logger?.LogInformation("AI operation interrupted by user (Escape key)");

            // Return to input state
            await context.RequestStateTransitionAsync(ChatState.Input);
        }
        catch (Exception ex)
        {
            context.Logger?.LogError(ex, "Error interrupting AI operation");
        }
    }

    private bool IsToolExecutionUpdate(ChatResponseUpdate responseUpdate)
    {
        if (responseUpdate.Contents == null)
        {
            // Check for tool response XML in text content even when Contents is null
            if (!string.IsNullOrEmpty(responseUpdate.Text))
            {
                var hasToolResponse = responseUpdate.Text.Contains("<tool_response") || responseUpdate.Text.Contains("</tool_response>");
                return hasToolResponse;
            }

            return false;
        }

        // Check for function call/result content
        var hasFunctionContent = responseUpdate.Contents.Any(content => content is FunctionCallContent or FunctionResultContent);
        if (hasFunctionContent)
        {
            return true;
        }

        // Check for tool response XML in text content
        if (!string.IsNullOrEmpty(responseUpdate.Text))
        {
            var hasToolResponse = responseUpdate.Text.Contains("<tool_response") || responseUpdate.Text.Contains("</tool_response>");
            return hasToolResponse;
        }

        return false;
    }

    private void ExtractToolNameFromUpdate(ITuiContext context, ChatResponseUpdate responseUpdate)
    {
        if (responseUpdate.Contents == null)
        {
            return;
        }

        var functionCall = responseUpdate.Contents.OfType<FunctionCallContent>().FirstOrDefault();
        if (functionCall != null)
        {
            var toolName = functionCall.Name ?? "Unknown Tool";

            // Create concise tool display with arrow format: toolName → keyValue
            if (functionCall.Arguments != null && functionCall.Arguments.Count > 0)
            {
                var keyValue = GetKeyArgumentValue(toolName, functionCall.Arguments);
                context.CurrentToolName = !string.IsNullOrEmpty(keyValue) ? $"{toolName} → {keyValue}" : toolName;
            }
            else
            {
                context.CurrentToolName = toolName;
            }

            // Store the mapping of call ID to tool name for later use
            context.FunctionCallToToolName[functionCall.CallId] = functionCall.Name!;

            // For EditTool, capture the pre-edit content
            _ = CapturePreEditContentForEditTool(context, functionCall);
        }
        else
        {
            context.CurrentToolName = string.Empty;
        }
    }

    private string GetKeyArgumentValue(string toolName, IDictionary<string, object?> arguments)
    {
        // Define key arguments for common tools to show the most relevant info
        var keyArguments = toolName.ToLowerInvariant() switch
        {
            "execute_command" or "shell" => new[] { "command", "cmd" },
            "write_file" or "writefile" or "write_to_file" => ["file_path", "path", "filename"],
            "read_file" or "readfile" or "read_file_tool" => ["file_path", "path", "filename"],
            "edit_file" or "editfile" => ["file_path", "path", "filename"],
            "grep" or "search" => ["pattern", "query", "search_term"],
            "ls" or "list" or "list_files" => ["path", "directory"],
            _ => ["path", "file", "command", "query", "name"] // fallback common keys
        };

        foreach (var key in keyArguments)
        {
            if (arguments.TryGetValue(key, out var value) && value != null)
            {
                var valueStr = value.ToString() ?? "";

                // For file paths, show just the filename if it's a full path
                if (key.Contains("path") || key.Contains("file"))
                {
                    valueStr = Path.GetFileName(valueStr);
                    if (string.IsNullOrEmpty(valueStr))
                    {
                        // If GetFileName returns empty, use the original but truncate
                        valueStr = value.ToString() ?? "";
                    }
                }

                // For commands, show first part (command name) and truncate if needed
                if (key.Contains("command") || key.Contains("cmd"))
                {
                    var parts = valueStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        valueStr = parts[0];
                        if (parts.Length > 1)
                        {
                            // Add indication there are more arguments
                            valueStr += "...";
                        }
                    }
                }

                // Final truncation to ensure single line (max 25 chars for the value part)
                if (valueStr.Length > 25)
                {
                    valueStr = valueStr[..22] + "...";
                }

                return valueStr;
            }
        }

        // If no key argument found, return empty to show just tool name
        return string.Empty;
    }

    private async Task CapturePreEditContentForEditTool(ITuiContext context, FunctionCallContent functionCall)
    {
        context.Logger?.LogDebug("=== CapturePreEditContentForEditTool START ===");

        try
        {
            // Only capture for EditTool
            if (!IsEditTool(functionCall.Name.ToLowerInvariant()))
            {
                context.Logger?.LogDebug("Not an EditTool, skipping pre-edit capture");
                return;
            }

            // Extract file_path from function arguments
            if (functionCall.Arguments?.TryGetValue("file_path", out var filePathValue) == true)
            {
                var filePath = filePathValue?.ToString();
                if (!string.IsNullOrEmpty(filePath))
                {
                    context.Logger?.LogDebug("Capturing pre-edit content for file: {FilePath}", filePath);

                    // Read the entire file content before the edit
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var preEditContent = await File.ReadAllTextAsync(filePath);
                            context.FunctionCallToPreEditContent[functionCall.CallId] = preEditContent;
                            context.Logger?.LogDebug("Captured pre-edit content, length: {Length}", preEditContent.Length);
                        }
                        catch (Exception ex)
                        {
                            context.Logger?.LogDebug(ex, "Failed to read pre-edit content from file: {FilePath}", filePath);
                        }
                    }
                    else
                    {
                        context.Logger?.LogDebug("File does not exist, storing empty pre-edit content: {FilePath}", filePath);
                        context.FunctionCallToPreEditContent[functionCall.CallId] = string.Empty;
                    }
                }
                else
                {
                    context.Logger?.LogDebug("File path is null or empty");
                }
            }
            else
            {
                context.Logger?.LogDebug("No file_path argument found in function call");
            }
        }
        catch (Exception ex)
        {
            context.Logger?.LogError(ex, "Error capturing pre-edit content");
        }

        context.Logger?.LogDebug("=== CapturePreEditContentForEditTool END ===");
    }

    private static bool IsEditTool(string normalizedToolName)
    {
        return normalizedToolName is "replace_in_file" or "edit_file" or "editfile" or "edit";
    }

    private async Task HandleToolExecutionResult(ITuiContext context, ChatResponseUpdate responseUpdate)
    {
        try
        {
            if (responseUpdate.Contents == null && string.IsNullOrEmpty(responseUpdate.Text))
            {
                return;
            }

            string? toolName = null;
            string? result = null;

            // Handle function call content (tool execution starting)
            var functionCall = responseUpdate.Contents?.OfType<FunctionCallContent>().FirstOrDefault();

            // Handle function result content (tool execution completed)
            var functionResult = responseUpdate.Contents?.OfType<FunctionResultContent>().FirstOrDefault();

            if (functionResult != null)
            {
                // Get the tool name from our stored mapping
                _ = context.FunctionCallToToolName.TryGetValue(functionResult.CallId, out toolName);
                toolName ??= "Unknown Tool";

                result = functionResult.Result?.ToString() ?? "";
            }
            // If we only have a function call but no result yet, don't process for display
            else if (functionCall != null && functionResult == null)
            {
                return;
            }
            // Handle XML tool responses in text content
            else if (!string.IsNullOrEmpty(responseUpdate.Text) &&
                     (responseUpdate.Text.Contains("<tool_response") || responseUpdate.Text.Contains("</tool_response>")))
            {
                result = responseUpdate.Text;

                // Extract tool name from XML if possible
                try
                {
                    var toolNameMatch = System.Text.RegularExpressions.Regex.Match(result, @"tool_name=""([^""]+)""");
                    toolName = toolNameMatch.Success ? toolNameMatch.Groups[1].Value : "Unknown Tool";
                }
                catch (Exception ex)
                {
                    context.Logger?.LogDebug(ex, "Failed to extract tool name from XML");
                    toolName = "Unknown Tool";
                }
            }

            // If we have a tool result to display
            if (!string.IsNullOrEmpty(result) && !string.IsNullOrEmpty(toolName))
            {
                // Parse the tool response for enhanced display
                var toolInfo = context.ToolResponseParser.ParseToolResponse(toolName, result);

                // Handle different tool types appropriately
                UnifiedDiff? diff = null;
                string? displayContent = null;

                try
                {
                    var normalizedToolName = toolInfo.ToolName.ToLowerInvariant();

                    // For WriteFileTool - show content directly, no diff
                    if (IsWriteFileTool(normalizedToolName))
                    {
                        displayContent = toolInfo.NewContent ?? ExtractContentFromXml(result);
                    }
                    // For EditTool and DiffPatchTools - generate/extract diffs
                    else if (IsEditTool(normalizedToolName) || IsDiffPatchTool(normalizedToolName))
                    {
                        if (!string.IsNullOrEmpty(toolInfo.FilePath))
                        {
                            string? originalContent = null;
                            var newContent = toolInfo.NewContent;

                            // For EditTool, use the captured pre-edit content
                            if (IsEditTool(normalizedToolName) && functionResult != null)
                            {
                                if (context.FunctionCallToPreEditContent.TryGetValue(functionResult.CallId, out var preEditContent))
                                {
                                    originalContent = preEditContent;
                                }
                            }
                            else
                            {
                                // For DiffPatchTools, try to read original content from file
                                if (File.Exists(toolInfo.FilePath))
                                {
                                    try
                                    {
                                        originalContent = await File.ReadAllTextAsync(toolInfo.FilePath);
                                    }
                                    catch (Exception ex)
                                    {
                                        context.Logger?.LogDebug(ex, "Could not read original file content for diff: {FilePath}", toolInfo.FilePath);
                                    }
                                }
                            }

                            // For EditTool, use content_on_disk as new content
                            // For DiffPatchTools, read current file content if needed
                            if (string.IsNullOrEmpty(newContent) && File.Exists(toolInfo.FilePath))
                            {
                                try
                                {
                                    newContent = await File.ReadAllTextAsync(toolInfo.FilePath);
                                }
                                catch (Exception ex)
                                {
                                    context.Logger?.LogDebug(ex, "Could not read current file content: {FilePath}", toolInfo.FilePath);
                                }
                            }

                            // Set up logger for UnifiedDiffGenerator debugging
                            UnifiedDiffGenerator.SetLogger(context.Logger);

                            // Generate diff using the tool response parser
                            diff = context.ToolResponseParser.ExtractFileDiff(
                                toolInfo.ToolName,
                                result,
                                originalContent,
                                newContent,
                                toolInfo.FilePath);
                        }
                    }
                    else
                    {
                        displayContent = toolInfo.Summary;
                    }
                }
                catch (Exception ex)
                {
                    context.Logger?.LogDebug(ex, "Could not process tool result for display");
                }

                // Create enhanced tool display with clean styling
                var toolDisplay = ToolExecutionDisplay.CreateToolDisplay(
                    toolInfo.ToolName,
                    toolInfo.Status,
                    toolInfo.Description,
                    diff: diff,
                    result: displayContent ?? toolInfo.Summary ?? result
                );

                // Display the tool execution result in scrollback
                context.ScrollbackTerminal.WriteStatic(toolDisplay);
                context.ScrollbackTerminal.WriteStatic(new Markup(""));
            }
        }
        catch (Exception ex)
        {
            context.Logger?.LogError(ex, "Error handling tool execution result");
        }

        await Task.CompletedTask;
    }

    private static bool IsWriteFileTool(string normalizedToolName)
    {
        return normalizedToolName is "write_file" or "writefile" or "write_to_file";
    }

    private static bool IsDiffPatchTool(string normalizedToolName)
    {
        return normalizedToolName is "apply_code_patch" or "generate_code_patch" or "preview_patch_application";
    }

    private static string? ExtractContentFromXml(string xmlResponse)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlResponse);

            // Look for content_on_disk element
            var contentNode = doc.SelectSingleNode("//content_on_disk");
            if (contentNode != null)
            {
                return contentNode.InnerText;
            }

            // Fallback: look for any content in notes
            var notesNode = doc.SelectSingleNode("//notes");
            if (notesNode != null)
            {
                return notesNode.InnerText?.Trim();
            }

            return null;
        }
        catch (XmlException)
        {
            // If XML parsing fails, return null
            return null;
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
}

#pragma warning restore IDE0010 // Add missing cases
