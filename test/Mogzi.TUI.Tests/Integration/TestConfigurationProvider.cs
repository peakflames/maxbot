using System.Text.Json;

namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Provides test-friendly configuration for integration tests.
/// Creates temporary configuration files and manages test-specific settings.
/// </summary>
public class TestConfigurationProvider : IDisposable
{
    private readonly string _tempConfigPath;
    private readonly ILogger<TestConfigurationProvider> _logger;
    private bool _isDisposed = false;

    public TestConfigurationProvider(ILogger<TestConfigurationProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tempConfigPath = Path.Combine(Path.GetTempPath(), $"mogzi-test-{Guid.NewGuid()}.json");
        
        CreateDefaultTestConfiguration();
    }

    /// <summary>
    /// Gets the path to the temporary test configuration file.
    /// </summary>
    public string ConfigPath => _tempConfigPath;

    /// <summary>
    /// Creates a test configuration with specified settings.
    /// </summary>
    public TestConfigurationProvider WithConfiguration(TestConfiguration config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        File.WriteAllText(_tempConfigPath, json);
        _logger.LogDebug("Created test configuration at: {ConfigPath}", _tempConfigPath);
        
        return this;
    }

    /// <summary>
    /// Creates a test configuration with a mock API provider.
    /// </summary>
    public TestConfigurationProvider WithMockProvider(string modelName = "test-model")
    {
        var config = new TestConfiguration
        {
            DefaultProfile = "test",
            Profiles = new Dictionary<string, TestProfile>
            {
                ["test"] = new TestProfile
                {
                    Provider = "mock",
                    Model = modelName,
                    ApiKey = "test-key",
                    BaseUrl = "http://localhost:8080/test",
                    MaxTokens = 1000,
                    Temperature = 0.7f
                }
            },
            ToolApprovals = "auto",
            LogLevel = "Debug"
        };

        return WithConfiguration(config);
    }

    /// <summary>
    /// Creates a test configuration that uses the real user configuration.
    /// This is useful for testing with actual API providers.
    /// </summary>
    public TestConfigurationProvider WithUserConfiguration()
    {
        var userConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "mogzi.config.json");
        
        if (File.Exists(userConfigPath))
        {
            var userConfig = File.ReadAllText(userConfigPath);
            File.WriteAllText(_tempConfigPath, userConfig);
            _logger.LogDebug("Copied user configuration to test config: {ConfigPath}", _tempConfigPath);
        }
        else
        {
            _logger.LogWarning("User configuration not found at: {UserConfigPath}, using default test configuration", userConfigPath);
            CreateDefaultTestConfiguration();
        }

        return this;
    }

    /// <summary>
    /// Creates a test configuration with offline mode (no API calls).
    /// </summary>
    public TestConfigurationProvider WithOfflineMode()
    {
        var config = new TestConfiguration
        {
            DefaultProfile = "offline",
            Profiles = new Dictionary<string, TestProfile>
            {
                ["offline"] = new TestProfile
                {
                    Provider = "offline",
                    Model = "offline-model",
                    ApiKey = "offline",
                    BaseUrl = "offline",
                    MaxTokens = 0,
                    Temperature = 0.0f
                }
            },
            ToolApprovals = "manual",
            LogLevel = "Debug"
        };

        return WithConfiguration(config);
    }

    /// <summary>
    /// Creates the default test configuration.
    /// </summary>
    private void CreateDefaultTestConfiguration()
    {
        WithMockProvider();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        
        try
        {
            if (File.Exists(_tempConfigPath))
            {
                File.Delete(_tempConfigPath);
                _logger.LogDebug("Deleted test configuration file: {ConfigPath}", _tempConfigPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete test configuration file: {ConfigPath}", _tempConfigPath);
        }
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Test configuration structure that matches the expected mogzi.config.json format.
/// </summary>
public class TestConfiguration
{
    public string DefaultProfile { get; set; } = "test";
    public Dictionary<string, TestProfile> Profiles { get; set; } = new();
    public string ToolApprovals { get; set; } = "auto";
    public string LogLevel { get; set; } = "Debug";
}

/// <summary>
/// Test profile configuration.
/// </summary>
public class TestProfile
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public float Temperature { get; set; }
}

/// <summary>
/// Factory for creating test configurations with common scenarios.
/// </summary>
public static class TestConfigurationFactory
{
    /// <summary>
    /// Creates a configuration for testing keyboard input without API calls.
    /// </summary>
    public static TestConfiguration CreateKeyboardTestConfig()
    {
        return new TestConfiguration
        {
            DefaultProfile = "keyboard-test",
            Profiles = new Dictionary<string, TestProfile>
            {
                ["keyboard-test"] = new TestProfile
                {
                    Provider = "mock",
                    Model = "keyboard-test-model",
                    ApiKey = "test-key",
                    BaseUrl = "http://localhost:8080/keyboard-test",
                    MaxTokens = 100,
                    Temperature = 0.0f
                }
            },
            ToolApprovals = "manual", // Prevent automatic tool execution during keyboard tests
            LogLevel = "Debug"
        };
    }

    /// <summary>
    /// Creates a configuration for testing rendering performance.
    /// </summary>
    public static TestConfiguration CreateRenderingTestConfig()
    {
        return new TestConfiguration
        {
            DefaultProfile = "rendering-test",
            Profiles = new Dictionary<string, TestProfile>
            {
                ["rendering-test"] = new TestProfile
                {
                    Provider = "mock",
                    Model = "rendering-test-model",
                    ApiKey = "test-key",
                    BaseUrl = "http://localhost:8080/rendering-test",
                    MaxTokens = 50,
                    Temperature = 0.0f
                }
            },
            ToolApprovals = "auto",
            LogLevel = "Debug"
        };
    }

    /// <summary>
    /// Creates a configuration for testing state transitions.
    /// </summary>
    public static TestConfiguration CreateStateTransitionTestConfig()
    {
        return new TestConfiguration
        {
            DefaultProfile = "state-test",
            Profiles = new Dictionary<string, TestProfile>
            {
                ["state-test"] = new TestProfile
                {
                    Provider = "mock",
                    Model = "state-test-model",
                    ApiKey = "test-key",
                    BaseUrl = "http://localhost:8080/state-test",
                    MaxTokens = 200,
                    Temperature = 0.1f
                }
            },
            ToolApprovals = "auto",
            LogLevel = "Debug"
        };
    }

    /// <summary>
    /// Creates a configuration for end-to-end workflow testing.
    /// </summary>
    public static TestConfiguration CreateWorkflowTestConfig()
    {
        return new TestConfiguration
        {
            DefaultProfile = "workflow-test",
            Profiles = new Dictionary<string, TestProfile>
            {
                ["workflow-test"] = new TestProfile
                {
                    Provider = "mock",
                    Model = "workflow-test-model",
                    ApiKey = "test-key",
                    BaseUrl = "http://localhost:8080/workflow-test",
                    MaxTokens = 500,
                    Temperature = 0.3f
                }
            },
            ToolApprovals = "auto",
            LogLevel = "Debug"
        };
    }

    /// <summary>
    /// Creates a configuration for performance testing.
    /// </summary>
    public static TestConfiguration CreatePerformanceTestConfig()
    {
        return new TestConfiguration
        {
            DefaultProfile = "performance-test",
            Profiles = new Dictionary<string, TestProfile>
            {
                ["performance-test"] = new TestProfile
                {
                    Provider = "mock",
                    Model = "performance-test-model",
                    ApiKey = "test-key",
                    BaseUrl = "http://localhost:8080/performance-test",
                    MaxTokens = 1000,
                    Temperature = 0.0f
                }
            },
            ToolApprovals = "auto",
            LogLevel = "Information" // Reduce log noise for performance tests
        };
    }
}

/// <summary>
/// Extension methods for TestConfigurationProvider to provide fluent API.
/// </summary>
public static class TestConfigurationProviderExtensions
{
    /// <summary>
    /// Configures the provider for keyboard input testing.
    /// </summary>
    public static TestConfigurationProvider ForKeyboardTesting(this TestConfigurationProvider provider)
    {
        return provider.WithConfiguration(TestConfigurationFactory.CreateKeyboardTestConfig());
    }

    /// <summary>
    /// Configures the provider for rendering testing.
    /// </summary>
    public static TestConfigurationProvider ForRenderingTesting(this TestConfigurationProvider provider)
    {
        return provider.WithConfiguration(TestConfigurationFactory.CreateRenderingTestConfig());
    }

    /// <summary>
    /// Configures the provider for state transition testing.
    /// </summary>
    public static TestConfigurationProvider ForStateTransitionTesting(this TestConfigurationProvider provider)
    {
        return provider.WithConfiguration(TestConfigurationFactory.CreateStateTransitionTestConfig());
    }

    /// <summary>
    /// Configures the provider for workflow testing.
    /// </summary>
    public static TestConfigurationProvider ForWorkflowTesting(this TestConfigurationProvider provider)
    {
        return provider.WithConfiguration(TestConfigurationFactory.CreateWorkflowTestConfig());
    }

    /// <summary>
    /// Configures the provider for performance testing.
    /// </summary>
    public static TestConfigurationProvider ForPerformanceTesting(this TestConfigurationProvider provider)
    {
        return provider.WithConfiguration(TestConfigurationFactory.CreatePerformanceTestConfig());
    }
}
