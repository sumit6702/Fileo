using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services;

public record BatchProgressState(
    int TotalFiles,
    int ProcessedFiles,
    int ConvertedCount,
    int SkippedCount,
    int FailedCount,
    long TotalOriginalBytes,
    long TotalConvertedBytes,
    TimeSpan ElapsedTime,
    TimeSpan EstimatedTimeRemaining,
    double FilesPerSecond,
    double OverallPercentage,
    string CurrentItemDisplay);

public interface IConversionService
{
    event Action<ConversionItem>? ItemStarted;
    event Action<ConversionItem>? ItemCompleted;
    event Action<BatchProgressState>? ProgressUpdated;

    bool IsPaused { get; }
    void Pause();
    void Resume();

    Task<BatchSummary> StartBatchAsync(
        IEnumerable<ImageFileInfo> files,
        string? inputRootFolder,
        ConversionSettings settings,
        IProgress<BatchProgressState>? progressReporter,
        CancellationToken cancellationToken);
}
