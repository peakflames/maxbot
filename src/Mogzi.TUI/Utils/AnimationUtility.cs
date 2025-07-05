namespace Mogzi.TUI.Utils;

/// <summary>
/// Provides consistent animation timing and frame calculation for UI components.
/// Ensures smooth, predictable animations across the application.
/// </summary>
public static class AnimationUtility
{
    /// <summary>
    /// Standard spinner frames using Unicode Braille patterns for smooth animation.
    /// </summary>
    private static readonly string[] DefaultSpinnerFrames = 
    [
        "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"
    ];

    /// <summary>
    /// Gets the current spinner frame using consistent timing.
    /// </summary>
    /// <param name="frameCount">Number of frames to cycle through (default: 8 for smooth animation)</param>
    /// <param name="intervalMs">Milliseconds between frame changes (default: 80ms for smooth motion)</param>
    /// <returns>The current spinner character</returns>
    public static string GetSpinnerFrame(int frameCount = 8, int intervalMs = 80)
    {
        if (frameCount <= 0 || frameCount > DefaultSpinnerFrames.Length)
        {
            frameCount = Math.Min(8, DefaultSpinnerFrames.Length);
        }

        if (intervalMs <= 0)
        {
            intervalMs = 80;
        }

        // Use ticks for consistent timing regardless of system state
        var animationFrame = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / intervalMs % frameCount;
        return DefaultSpinnerFrames[animationFrame];
    }

    /// <summary>
    /// Gets a specific spinner frame by index.
    /// </summary>
    /// <param name="frameIndex">The frame index (0-based)</param>
    /// <returns>The spinner character at the specified index</returns>
    public static string GetSpinnerFrameByIndex(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= DefaultSpinnerFrames.Length)
        {
            frameIndex = 0;
        }

        return DefaultSpinnerFrames[frameIndex];
    }

    /// <summary>
    /// Gets the total number of available spinner frames.
    /// </summary>
    public static int FrameCount => DefaultSpinnerFrames.Length;
}
