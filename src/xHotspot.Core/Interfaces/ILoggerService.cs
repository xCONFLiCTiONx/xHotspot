namespace xHotspot.Core.Interfaces;

public interface ILoggerService
{
    void LogTrace(string message);
    void LogDebug(string message);
    void LogInformation(string message);
    void LogWarning(string message, Exception? ex = null);
    void LogError(string message, Exception? ex = null);
    void LogCritical(string message, Exception? ex = null);
    IReadOnlyList<string> GetRecentLogs(int count = 100);
}
