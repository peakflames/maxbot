using FluentResults;
using MaxBot;
using Microsoft.Extensions.AI;

namespace CLI;

public class App
{
    private ChatClient maxClient;
    private bool showStatus;
    // private string aiName = "Max";


    public App(ChatClient maxClient, bool showStatus)
    {
        this.showStatus = showStatus;
        this.maxClient = maxClient;
    }

    public static void ConsoleWriteLLMResponseDetails(string response)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n{response}");
        Console.ForegroundColor = originalColor;
    }

    public static void ConsoleWriteError(Result result)
    {
        var temp = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {result.Errors.FirstOrDefault()?.Message}");
        Console.ForegroundColor = temp;
    }

    public async Task<int> Run(string activeMode, string? userPrompt = null)
    {
        var retval = 0;

        if (activeMode == "chat")
        {
            retval = await StartChatAsync();
        }
        else if (activeMode == "oneshot")
        {
            retval = await StartOneShotAsync(userPrompt);
        }
        else
        {
            Console.WriteLine($"Invalid mode: {activeMode}");
            return 1;
        }

        return retval;
    }

    public async Task<int> StartChatAsync()
    {
        try
        {
            var robotEmoji = char.ConvertFromUtf32(0x1F916);  // 🤖
            var folderEmoji = char.ConvertFromUtf32(0x1F4C2); // 📂
            List<ChatMessage> chatHistory =
            [
                new(ChatRole.System, maxClient.SystemPrompt),
            ];  

            // -------------------------------------------------------------------------------------------
            // Conversational Chat with the AI
            // -------------------------------------------------------------------------------------------
            while (true)
            {
                // Get user prompt and add to chat history
                Console.ForegroundColor = ConsoleColor.Yellow;
                
                // Get the name of the current working directory, but just the final part of the path
                var cwd = Directory.GetCurrentDirectory();

                Console.Write($"\n{robotEmoji} Max | {folderEmoji} {cwd}\n% ");    
                Console.ForegroundColor = ConsoleColor.White;
                var userPrompt = Console.ReadLine();
                Console.WriteLine();
                
                Console.ForegroundColor = ConsoleColor.Blue;
                // if the user prompt is empty, "exit", or "quit", then exit the chat loop
                if (string.IsNullOrWhiteSpace(userPrompt) || userPrompt.ToLower() == "exit" || userPrompt.ToLower() == "quit")
                    break;

                chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

                // Stream the AI Response and add to chat history            
                // Console.WriteLine("Sending API Request...");
                // Console.WriteLine($"\n{aiName}:");
                var response = "";
                await foreach (var item in maxClient.ChatClientMEAI.GetStreamingResponseAsync(chatHistory, maxClient.ChatOptions))
                {
                    Console.Write(item.Text);
                    response += item.Text;
                }
                chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
                Console.WriteLine();

                if (showStatus)
                {
                    WriteTokenMetrics(chatHistory);
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        return 0;
    }

    public async Task<int> StartOneShotAsync(string? userPrompt)
    {
        try
        {
            List<ChatMessage> chatHistory =
            [
                new(ChatRole.System, maxClient.SystemPrompt),
            ];

            // if the user prompt is empty, "exit", or "quit", then exit the chat loop
            if (string.IsNullOrWhiteSpace(userPrompt) || userPrompt.ToLower() == "exit" || userPrompt.ToLower() == "quit")
            {
                return 0;
            }

            chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

            var response = "";
            await foreach (var item in maxClient.ChatClientMEAI.GetStreamingResponseAsync(chatHistory, maxClient.ChatOptions))
            {
                Console.Write(item.Text);
                response += item.Text;
            }
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
            Console.WriteLine();

            if (showStatus)
            {
                WriteTokenMetrics(chatHistory);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        return 0;
    }

    private static void WriteTokenMetrics(List<ChatMessage> chatHistory)
    {   
        var currentForegroundColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;

        // calculate the percentage of tokens used against the 200K limit
        var tokenCount = MaxBot.Utils.ApiMetricUtils.GetSimplisticTokenCount(chatHistory);
        var percentage = (double)tokenCount / 200000 * 100;
        // if (OPENAI_API_MODEL!.Contains("claude"))
        // {
        //     Console.WriteLine($"[I/O Tokens Used: {tokenCount} of 200K, {percentage:N2}%]");
        // }
        // else
        // {
        Console.WriteLine($"[Tokens Used: {tokenCount}]");
        // }
        Console.ForegroundColor = currentForegroundColor;
    }
}