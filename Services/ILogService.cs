using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services;

public interface ILogService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
    string GetLogDirectory();
    Task ExportResultsToCsvAsync(string targetFilePath, IEnumerable<ConversionItem> items);
    void ShowToastNotification(string title, string message);
}
