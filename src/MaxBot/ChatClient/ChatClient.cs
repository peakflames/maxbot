using FluentResults;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MaxBot.Domain;

namespace MaxBot;

public partial class ChatClient
{
    public IChatClient ChatClientMEAI { get; init; }
    public MaxbotConfiguration Config { get; init; }
    public string SystemPrompt { get; init; }
    public Profile ActiveProfile { get; init; }
    public ApiProvider ActiveApiProvider { get; init; }

    public PlatformID OperatingSystem { get; init; }
    public string DefaultShell { get; init; }
    public string Username { get; init; }
    public string Hostname { get; init; }

    private ChatClient(IChatClient chatClient, MaxbotConfiguration config, Profile activeProfile, ApiProvider activeApiProvider)
    {
        ChatClientMEAI = chatClient;
        Config = config;
        ActiveProfile = activeProfile;
        ActiveApiProvider = activeApiProvider;

        // Detect the current operating system, we need to handle Windows, MacOS, and Linux differently
        OperatingSystem = Environment.OSVersion.Platform;
        DefaultShell = OperatingSystem switch {
            PlatformID.Win32NT => "powershell",
            PlatformID.MacOSX => "zsh",
            _ => "bash"
        };

        // Get username
        Username = Environment.UserName;

        // Get hostname
        Hostname = System.Net.Dns.GetHostName();

        SystemPrompt = Promptinator.GetSystemPrompt(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                                   OperatingSystem.ToString(),
                                                   DefaultShell,
                                                   Username,
                                                   Hostname);
    }

    
    public static Result<ChatClient> Create(string configFilePath, string? profileName = null)
    {
        string jsonContent;
        try
        {
            jsonContent = File.ReadAllText(configFilePath);
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to read config file: {ex.Message}");
        }

        MaxbotConfigurationRoot? configRoot;
        try
        {
            configRoot = JsonSerializer.Deserialize<MaxbotConfigurationRoot>(
                jsonContent, 
                MaxbotConfigurationContext.Default.MaxbotConfigurationRoot);
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to parse config file: {ex.Message}");
        }

        var maxbotConfig = configRoot?.MaxbotConfig;
        if (maxbotConfig is null)
        {
            return Result.Fail($"While reading the config '{configFilePath}', was not able to find the 'maxbotConfig' section.");
        }


        // Find the specified profile, or default profile, or first profile
        Profile? profile;
        if (!string.IsNullOrEmpty(profileName))
        {
            profile = maxbotConfig.Profiles.FirstOrDefault(p => p.Name == profileName);
            if (profile is null)
            {
                return Result.Fail($"Profile '{profileName}' not found in configuration.");
            }
        }
        else
        {
            profile = maxbotConfig.Profiles.FirstOrDefault(p => p.Default) ?? maxbotConfig.Profiles.FirstOrDefault();
            if (profile is null)
            {
                return Result.Fail($"No profiles found in the configuration.");
            }
        }

        // Find the corresponding API provider
        var apiProvider = maxbotConfig.ApiProviders.FirstOrDefault(p => p.Name == profile.ApiProvider);
        if (apiProvider is null)
        {
            return Result.Fail($"API provider '{profile.ApiProvider}' specified in profile '{profile.Name}' not found.");
        }

        string apiKey = apiProvider.ApiKey;
        string baseUrl = apiProvider.BaseUrl;
        string modelId = profile.ModelId;

        var chatClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { 
                Endpoint = new(baseUrl)
            })
            .AsChatClient(modelId);

        return new ChatClient(chatClient, maxbotConfig, profile, apiProvider);
    }
}
