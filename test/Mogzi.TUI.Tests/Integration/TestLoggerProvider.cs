using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Mogzi.TUI.Tests.Integration;

/// <summary>
/// Test logger provider that captures log messages for integration testing.
/// </summary>
public class TestLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minLogLevel;
    private readonly ConcurrentDictionary<string, TestLogger> _loggers = new();

    public TestLoggerProvider(LogLevel minLogLevel = LogLevel.Debug)
    {
        _minLogLevel = minLogLevel;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new TestLogger(name, _minLogLevel));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }

    /// <summary>
    /// Gets all captured log entries across all loggers.
    /// </summary>
    public IReadOnlyList<TestLogEntry> GetAllLogEntries()
    {
        return _loggers.Values
            .SelectMany(logger => logger.LogEntries)
            .OrderBy(entry => entry.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Gets log entries for a specific category.
    /// </summary>
    public IReadOnlyList<TestLogEntry> GetLogEntries(string categoryName)
    {
        return _loggers.TryGetValue(categoryName, out var logger) 
            ? logger.LogEntries 
            : Array.Empty<TestLogEntry>();
    }

    /// <summary>
    /// Clears all captured log entries.
    /// </summary>
    public void ClearLogs()
    {
        foreach (var logger in _loggers.Values)
        {
            logger.ClearLogs();
        }
    }
}

/// <summary>
/// Test logger that captures log messages.
/// </summary>
public class TestLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LogLevel _minLogLevel;
    private readonly List<TestLogEntry> _logEntries = new();
    private readonly object _lock = new();

    public TestLogger(string categoryName, LogLevel minLogLevel)
    {
        _categoryName = categoryName;
        _minLogLevel = minLogLevel;
    }

    public IReadOnlyList<TestLogEntry> LogEntries
    {
        get
        {
            lock (_lock)
            {
                return _logEntries.ToList();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _minLogLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var entry = new TestLogEntry(
            DateTime.UtcNow,
            logLevel,
            _categoryName,
            eventId,
            message,
            exception
        );

        lock (_lock)
        {
            _logEntries.Add(entry);
        }
    }

    public void ClearLogs()
    {
        lock (_lock)
        {
            _logEntries.Clear();
        }
    }
}

/// <summary>
/// Represents a captured log entry for testing.
/// </summary>
public record TestLogEntry(
    DateTime Timestamp,
    LogLevel LogLevel,
    string CategoryName,
    EventId EventId,
    string Message,
    Exception? Exception
);
