using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;

namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Captures and analyzes UI rendering output for integration testing.
/// Provides methods to verify component visibility, content, and rendering behavior.
/// </summary>
public class RenderingCapture
{
    private readonly ILogger<RenderingCapture> _logger;
    private readonly List<RenderSnapshot> _snapshots = new();
    private readonly object _lock = new();

    public RenderingCapture(ILogger<RenderingCapture> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all captured render snapshots.
    /// </summary>
    public IReadOnlyList<RenderSnapshot> Snapshots
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.ToList();
            }
        }
    }

    /// <summary>
    /// Captures a render snapshot from a renderable object.
    /// </summary>
    public RenderSnapshot CaptureSnapshot(IRenderable renderable, string? description = null)
    {
        _logger.LogDebug("Capturing render snapshot: {Description}", description ?? "No description");

        var content = RenderToString(renderable);
        var snapshot = new RenderSnapshot(
            DateTime.UtcNow,
            content,
            description,
            AnalyzeContent(content),
            ExtractComponentInfo(renderable)
        );

        lock (_lock)
        {
            _snapshots.Add(snapshot);
        }

        return snapshot;
    }

    /// <summary>
    /// Captures a snapshot from the component manager's current layout.
    /// </summary>
    public RenderSnapshot CaptureLayoutSnapshot(ITuiComponentManager componentManager, IRenderContext context, string? description = null)
    {
        var renderable = componentManager.RenderLayout(context);
        return CaptureSnapshot(renderable, description ?? "Layout snapshot");
    }

    /// <summary>
    /// Verifies that a component is visible in the latest snapshot.
    /// </summary>
    public bool IsComponentVisible(string componentName)
    {
        var latestSnapshot = GetLatestSnapshot();
        return latestSnapshot?.ComponentInfo.Any(c => c.Name == componentName && c.IsVisible) ?? false;
    }

    /// <summary>
    /// Verifies that specific text content appears in the latest snapshot.
    /// </summary>
    public bool ContainsText(string text, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        var latestSnapshot = GetLatestSnapshot();
        return latestSnapshot?.Content.Contains(text, comparison) ?? false;
    }

    /// <summary>
    /// Verifies that a pattern matches in the latest snapshot.
    /// </summary>
    public bool MatchesPattern(string pattern)
    {
        var latestSnapshot = GetLatestSnapshot();
        if (latestSnapshot == null) return false;

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(latestSnapshot.Content, pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error matching pattern: {Pattern}", pattern);
            return false;
        }
    }

    /// <summary>
    /// Gets the number of times content has changed between snapshots.
    /// </summary>
    public int GetContentChangeCount()
    {
        lock (_lock)
        {
            if (_snapshots.Count <= 1) return 0;

            int changes = 0;
            for (int i = 1; i < _snapshots.Count; i++)
            {
                if (_snapshots[i].Content != _snapshots[i - 1].Content)
                {
                    changes++;
                }
            }
            return changes;
        }
    }

    /// <summary>
    /// Gets the latest render snapshot.
    /// </summary>
    public RenderSnapshot? GetLatestSnapshot()
    {
        lock (_lock)
        {
            return _snapshots.LastOrDefault();
        }
    }

    /// <summary>
    /// Gets snapshots within a specific time range.
    /// </summary>
    public IReadOnlyList<RenderSnapshot> GetSnapshotsInRange(DateTime start, DateTime end)
    {
        lock (_lock)
        {
            return _snapshots
                .Where(s => s.Timestamp >= start && s.Timestamp <= end)
                .ToList();
        }
    }

    /// <summary>
    /// Clears all captured snapshots.
    /// </summary>
    public void ClearSnapshots()
    {
        lock (_lock)
        {
            _snapshots.Clear();
        }
        _logger.LogDebug("Cleared all render snapshots");
    }

    /// <summary>
    /// Analyzes rendering performance based on captured snapshots.
    /// </summary>
    public RenderingPerformanceAnalysis AnalyzePerformance()
    {
        lock (_lock)
        {
            if (_snapshots.Count == 0)
            {
                return new RenderingPerformanceAnalysis(0, TimeSpan.Zero, 0, 0);
            }

            var totalTime = _snapshots.Last().Timestamp - _snapshots.First().Timestamp;
            var averageInterval = _snapshots.Count > 1 
                ? TimeSpan.FromTicks(totalTime.Ticks / (_snapshots.Count - 1))
                : TimeSpan.Zero;

            var contentChanges = GetContentChangeCount();
            var redundantRenders = _snapshots.Count - contentChanges - 1; // -1 for initial render

            return new RenderingPerformanceAnalysis(
                _snapshots.Count,
                averageInterval,
                contentChanges,
                Math.Max(0, redundantRenders)
            );
        }
    }

    /// <summary>
    /// Renders an IRenderable to a string for analysis.
    /// </summary>
    private static string RenderToString(IRenderable renderable)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(new StringWriter())
        });

        var segments = renderable.Render(new RenderOptions(console.Profile.Capabilities, new Size(80, 24)), 80);
        var builder = new StringBuilder();

        foreach (var segment in segments)
        {
            builder.Append(segment.Text);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Analyzes content for common patterns and metrics.
    /// </summary>
    private static ContentAnalysis AnalyzeContent(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.None);
        var nonEmptyLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).Count();
        var totalCharacters = content.Length;
        var hasInput = content.Contains(">") || content.Contains("Input:");
        
        // More specific progress indicator detection to avoid false positives
        // Look for actual progress indicators, not just any percentage
        var hasProgress = content.Contains("Progress:") || 
                         content.Contains("Loading") || 
                         content.Contains("Processing") ||
                         content.Contains("Working") ||
                         (content.Contains("%") && !content.Contains("context left"));
        
        var hasError = content.Contains("Error") || content.Contains("Exception");

        return new ContentAnalysis(
            lines.Length,
            nonEmptyLines,
            totalCharacters,
            hasInput,
            hasProgress,
            hasError
        );
    }

    /// <summary>
    /// Extracts component information from a renderable if possible.
    /// </summary>
    private static List<ComponentInfo> ExtractComponentInfo(IRenderable renderable)
    {
        var components = new List<ComponentInfo>();

        // This is a simplified extraction - in a real implementation,
        // we would need to traverse the renderable tree to find components
        
        // For now, we'll create a basic analysis based on the renderable type
        var componentName = renderable.GetType().Name;
        components.Add(new ComponentInfo(componentName, true, renderable.GetType().FullName ?? componentName));

        return components;
    }
}

/// <summary>
/// Represents a captured render snapshot.
/// </summary>
public record RenderSnapshot(
    DateTime Timestamp,
    string Content,
    string? Description,
    ContentAnalysis Analysis,
    List<ComponentInfo> ComponentInfo
);

/// <summary>
/// Analysis of rendered content.
/// </summary>
public record ContentAnalysis(
    int TotalLines,
    int NonEmptyLines,
    int TotalCharacters,
    bool HasInputIndicator,
    bool HasProgressIndicator,
    bool HasErrorIndicator
);

/// <summary>
/// Information about a component in the render.
/// </summary>
public record ComponentInfo(
    string Name,
    bool IsVisible,
    string Type
);

/// <summary>
/// Analysis of rendering performance.
/// </summary>
public record RenderingPerformanceAnalysis(
    int TotalRenders,
    TimeSpan AverageRenderInterval,
    int ContentChanges,
    int RedundantRenders
)
{
    /// <summary>
    /// Gets the percentage of renders that resulted in content changes.
    /// </summary>
    public double EfficiencyPercentage => TotalRenders > 0 
        ? (double)ContentChanges / TotalRenders * 100 
        : 0;

    /// <summary>
    /// Gets whether the rendering appears to be in a loop (too many redundant renders).
    /// </summary>
    public bool HasRenderingLoop => RedundantRenders > 10 && EfficiencyPercentage < 20;

    /// <summary>
    /// Gets the estimated renders per second.
    /// </summary>
    public double RendersPerSecond => AverageRenderInterval.TotalSeconds > 0 
        ? 1.0 / AverageRenderInterval.TotalSeconds 
        : 0;
}

/// <summary>
/// Extension methods for RenderingCapture to provide fluent assertions.
/// </summary>
public static class RenderingCaptureExtensions
{
    /// <summary>
    /// Asserts that a component is visible in the latest snapshot.
    /// </summary>
    public static RenderingCapture ShouldShowComponent(this RenderingCapture capture, string componentName)
    {
        if (!capture.IsComponentVisible(componentName))
        {
            throw new AssertionException($"Component '{componentName}' should be visible but was not found in the latest snapshot");
        }
        return capture;
    }

    /// <summary>
    /// Asserts that specific text content appears in the latest snapshot.
    /// </summary>
    public static RenderingCapture ShouldContainText(this RenderingCapture capture, string text)
    {
        if (!capture.ContainsText(text))
        {
            throw new AssertionException($"Content should contain '{text}' but it was not found in the latest snapshot");
        }
        return capture;
    }

    /// <summary>
    /// Asserts that the rendering is not in a loop.
    /// </summary>
    public static RenderingCapture ShouldNotHaveRenderingLoop(this RenderingCapture capture)
    {
        var analysis = capture.AnalyzePerformance();
        if (analysis.HasRenderingLoop)
        {
            throw new AssertionException($"Rendering loop detected: {analysis.RedundantRenders} redundant renders out of {analysis.TotalRenders} total renders ({analysis.EfficiencyPercentage:F1}% efficiency)");
        }
        return capture;
    }
}

/// <summary>
/// Custom exception for rendering assertions.
/// </summary>
public class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
    public AssertionException(string message, Exception innerException) : base(message, innerException) { }
}
