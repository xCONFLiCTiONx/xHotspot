using System.Collections.Concurrent;
using xHotspot.Core.Interfaces;

namespace xHotspot.Service.Services;

public class LoggerService : ILoggerService
{
    private readonly ConcurrentQueue<string> _logs = new();
    private const int MaxLogs = 1000;

    public void LogTrace(string message) => Append("TRACE", message);
    public void LogDebug(string message) => Append("DEBUG", message);
    public void LogInformation(string message) => Append("INFO", message);
    public void LogWarning(string message, Exception? ex = null) => Append("WARN", $"{message} {(ex != null ? ex.Message : "")}");
    public void LogError(string message, Exception? ex = null) => Append("ERROR", $"{message} {(ex != null ? ex.ToString() : "")}");
    public void LogCritical(string message, Exception? ex = null) => Append("CRITICAL", $"{message} {(ex != null ? ex.ToString() : "")}");

    private void Append(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        _logs.Enqueue(line);
        while (_logs.Count > MaxLogs && _logs.TryDequeue(out _)) { }
        Console.WriteLine(line);
    }

    public IReadOnlyList<string> GetRecentLogs(int count = 100)
    {
        return _logs.TakeLast(count).ToList();
    }
}
