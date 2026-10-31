using System.Diagnostics;
using System.IO;
using System.Text;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services;

public class LogService : ILogService
{
    private readonly string _logDirectory;
    private readonly string _currentLogFile;
    private readonly object _lock = new();

    public LogService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _logDirectory = Path.Combine(appData, "Fileo", "logs");
        Directory.CreateDirectory(_logDirectory);

        string today = DateTime.Now.ToString("yyyy-MM-dd");
        _currentLogFile = Path.Combine(_logDirectory, $"fileo_{today}.log");
    }

    public string GetLogDirectory() => _logDirectory;

    public void LogInfo(string message) => WriteLog("INFO", message);
    public void LogWarning(string message) => WriteLog("WARN", message);

    public void LogError(string message, Exception? ex = null)
    {
        string text = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
        WriteLog("ERROR", text);
    }

    private void WriteLog(string level, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string line = $"[{timestamp}] [{level}] {message}";

        try
        {
            lock (_lock)
            {
                File.AppendAllText(_currentLogFile, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Do not throw from logger
        }
    }

    public async Task ExportResultsToCsvAsync(string targetFilePath, IEnumerable<ConversionItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ID,FileName,RelativePath,InputFormat,OutputFormat,OriginalSize,OutputSize,SavedBytes,SavedPercent,Status,DurationMs,ErrorMessage,OutputPath,BackupPath");

        foreach (var item in items)
        {
            sb.Append(EscapeCsv(item.Id)).Append(',');
            sb.Append(EscapeCsv(item.FileName)).Append(',');
            sb.Append(EscapeCsv(item.RelativePath)).Append(',');
            sb.Append(EscapeCsv(item.InputFormat)).Append(',');
            sb.Append(EscapeCsv(item.OutputFormat)).Append(',');
            sb.Append(item.OriginalSize).Append(',');
            sb.Append(item.OutputSize).Append(',');
            sb.Append(item.SavedBytes).Append(',');
            sb.Append(item.SavedPercent).Append(',');
            sb.Append(EscapeCsv(item.Status.ToString())).Append(',');
            sb.Append(item.DurationMs).Append(',');
            sb.Append(EscapeCsv(item.ErrorMessage ?? string.Empty)).Append(',');
            sb.Append(EscapeCsv(item.OutputPath ?? string.Empty)).Append(',');
            sb.Append(EscapeCsv(item.BackupPath ?? string.Empty));
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(targetFilePath, sb.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return $"\"{value}\"";
    }

    public void ShowToastNotification(string title, string message)
    {
        try
        {
            // Use PowerShell to trigger native Windows notification
            string escapedTitle = title.Replace("\"", "`\"");
            string escapedMessage = message.Replace("\"", "`\"");
            string script = $"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; " +
                            $"[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null; " +
                            $"$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02); " +
                            $"$texts = $template.GetElementsByTagName('text'); " +
                            $"$texts[0].AppendChild($template.CreateTextNode(\"{escapedTitle}\")) | Out-Null; " +
                            $"$texts[1].AppendChild($template.CreateTextNode(\"{escapedMessage}\")) | Out-Null; " +
                            $"$toast = [Windows.UI.Notifications.ToastNotification]::new($template); " +
                            $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Fileo').Show($toast);";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            LogError("Failed to show Windows toast notification", ex);
        }
    }
}
