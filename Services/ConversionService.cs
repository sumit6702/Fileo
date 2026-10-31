using System.Diagnostics;
using System.IO;
using ImageBatchConverter.Models;
using ImageBatchConverter.Services.Converters;

namespace ImageBatchConverter.Services;

public class ConversionService : IConversionService
{
    private readonly ILogService _logService;
    private readonly IBackupService _backupService;
    private readonly Dictionary<OutputFormatType, IImageConverter> _converters;

    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private volatile bool _isPaused = false;

    public bool IsPaused => _isPaused;

    public event Action<ConversionItem>? ItemStarted;
    public event Action<ConversionItem>? ItemCompleted;
    public event Action<BatchProgressState>? ProgressUpdated;

    public ConversionService(
        ILogService logService,
        IBackupService backupService,
        IEnumerable<IImageConverter> converters)
    {
        _logService = logService;
        _backupService = backupService;
        _converters = converters.ToDictionary(c => c.Format);
    }

    public void Pause()
    {
        _isPaused = true;
        _pauseEvent.Reset();
        _logService.LogInfo("Batch conversion paused by user.");
    }

    public void Resume()
    {
        _isPaused = false;
        _pauseEvent.Set();
        _logService.LogInfo("Batch conversion resumed by user.");
    }

    public async Task<BatchSummary> StartBatchAsync(
        IEnumerable<ImageFileInfo> files,
        string? inputRootFolder,
        ConversionSettings settings,
        IProgress<BatchProgressState>? progressReporter,
        CancellationToken cancellationToken)
    {
        var fileList = files.ToList();
        int totalFiles = fileList.Count;

        string targetExt = FormatExtensions.GetExtensionForOutput(settings.TargetFormat);
        if (!_converters.TryGetValue(settings.TargetFormat, out var converter))
        {
            throw new InvalidOperationException($"No converter registered for {settings.TargetFormat}");
        }

        // Prepare backup folder if backup is enabled
        string backupFolder = string.Empty;
        if (settings.BackupOriginals)
        {
            backupFolder = !string.IsNullOrWhiteSpace(settings.BackupFolder)
                ? settings.BackupFolder
                : _backupService.GenerateDefaultBackupFolderName(inputRootFolder);

            Directory.CreateDirectory(backupFolder);
            _logService.LogInfo($"Backup folder prepared: {backupFolder}");
        }

        int processed = 0;
        int converted = 0;
        int skipped = 0;
        int failed = 0;
        long totalOriginalBytes = 0;
        long totalConvertedBytes = 0;

        var stopwatch = Stopwatch.StartNew();
        int maxWorkers = Math.Clamp(settings.WorkerCount, 1, 64);
        using var throttle = new SemaphoreSlim(maxWorkers, maxWorkers);

        _logService.LogInfo($"Starting batch conversion: {totalFiles} files, Target: {settings.TargetFormat}, Workers: {maxWorkers}");

        var tasks = fileList.Select(async file =>
        {
            await throttle.WaitAsync(cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Wait if paused
                while (!_pauseEvent.IsSet)
                {
                    await Task.Delay(150, cancellationToken);
                }

                var item = new ConversionItem
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    InputPath = file.FullPath,
                    RelativePath = file.RelativePath,
                    FileName = file.FileName,
                    InputFormat = file.FormatName,
                    OutputFormat = FormatExtensions.GetFormatDisplayName(settings.TargetFormat),
                    OriginalSize = file.FileSizeBytes,
                    Status = ConversionStatus.Converting,
                    Progress = 0
                };

                ItemStarted?.Invoke(item);
                var itemSw = Stopwatch.StartNew();

                await ProcessSingleItemAsync(item, file, inputRootFolder, settings, converter, backupFolder, cancellationToken);

                itemSw.Stop();
                item.DurationMs = itemSw.ElapsedMilliseconds;

                // Update counters
                lock (fileList)
                {
                    processed++;
                    if (item.Status == ConversionStatus.Success)
                    {
                        converted++;
                        totalOriginalBytes += item.OriginalSize;
                        totalConvertedBytes += item.OutputSize;
                    }
                    else if (item.Status == ConversionStatus.Skipped)
                    {
                        skipped++;
                    }
                    else
                    {
                        failed++;
                    }

                    // Progress calculations
                    double elapsedSec = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                    double fps = processed / elapsedSec;
                    int remaining = totalFiles - processed;
                    var eta = fps > 0 ? TimeSpan.FromSeconds(remaining / fps) : TimeSpan.Zero;
                    double pct = totalFiles > 0 ? Math.Round(((double)processed / totalFiles) * 100.0, 1) : 0;

                    string display = $"{item.FileName} -> {Path.GetFileName(item.OutputPath ?? "")}";

                    var state = new BatchProgressState(
                        totalFiles,
                        processed,
                        converted,
                        skipped,
                        failed,
                        totalOriginalBytes,
                        totalConvertedBytes,
                        stopwatch.Elapsed,
                        eta,
                        fps,
                        pct,
                        display);

                    progressReporter?.Report(state);
                    ProgressUpdated?.Invoke(state);
                }

                ItemCompleted?.Invoke(item);
            }
            finally
            {
                throttle.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            _logService.LogWarning("Batch conversion canceled by user.");
            throw;
        }

        stopwatch.Stop();
        _logService.LogInfo($"Batch finished in {stopwatch.Elapsed:mm\\:ss}. Converted: {converted}, Skipped: {skipped}, Failed: {failed}");

        return new BatchSummary
        {
            TotalFiles = totalFiles,
            ConvertedCount = converted,
            SkippedCount = skipped,
            FailedCount = failed,
            TotalOriginalBytes = totalOriginalBytes,
            TotalConvertedBytes = totalConvertedBytes,
            ElapsedTime = stopwatch.Elapsed,
            OutputFolder = settings.SaveInSameFolder
                ? (inputRootFolder ?? "")
                : (!string.IsNullOrWhiteSpace(settings.CustomOutputFolder) ? settings.CustomOutputFolder : Path.Combine(inputRootFolder ?? "", "Converted")),
            BackupFolder = backupFolder
        };
    }

    private async Task ProcessSingleItemAsync(
        ConversionItem item,
        ImageFileInfo file,
        string? inputRootFolder,
        ConversionSettings settings,
        IImageConverter converter,
        string backupFolder,
        CancellationToken cancellationToken)
    {
        string targetExt = FormatExtensions.GetExtensionForOutput(settings.TargetFormat);

        // 1. Determine destination path
        string originalDir = Path.GetDirectoryName(file.FullPath) ?? string.Empty;
        string targetDir;
        if (settings.SaveInSameFolder)
        {
            targetDir = originalDir;
        }
        else
        {
            string baseFolder = !string.IsNullOrWhiteSpace(settings.CustomOutputFolder)
                ? settings.CustomOutputFolder
                : Path.Combine(inputRootFolder ?? originalDir, "Converted");

            string relDir = Path.GetDirectoryName(file.RelativePath) ?? string.Empty;
            targetDir = string.IsNullOrEmpty(relDir) ? baseFolder : Path.Combine(baseFolder, relDir);
        }

        Directory.CreateDirectory(targetDir);

        string baseName = Path.GetFileNameWithoutExtension(file.FileName);
        string newFileName = $"{settings.Prefix}{baseName}{settings.Suffix}{targetExt}";
        string targetPath = Path.Combine(targetDir, newFileName);

        // 2. Overwrite check
        if (File.Exists(targetPath))
        {
            if (settings.OverwriteMode == OverwriteBehavior.Skip)
            {
                item.Status = ConversionStatus.Skipped;
                item.ErrorMessage = "Destination file already exists (Skip overwrite mode).";
                item.OutputPath = targetPath;
                return;
            }

            if (settings.OverwriteMode == OverwriteBehavior.AutoRename)
            {
                targetPath = GenerateUniqueFilePath(targetDir, settings.Prefix + baseName + settings.Suffix, targetExt);
            }
        }

        item.OutputPath = targetPath;

        // 3. Backup original if enabled
        bool backupVerified = false;
        if (settings.BackupOriginals)
        {
            var (backupSuccess, backupPath, backupError) = await _backupService.CreateBackupAsync(
                file.FullPath,
                file.RelativePath,
                backupFolder,
                cancellationToken);

            item.BackupPath = backupPath;

            if (!backupSuccess)
            {
                item.Status = ConversionStatus.Failed;
                item.ErrorMessage = $"Backup failed: {backupError}. Conversion halted for safety.";
                _logService.LogError($"Original backup failed for {file.FullPath}: {backupError}");
                return;
            }

            backupVerified = true;
        }

        // 4. Perform image conversion
        var conversionResult = await converter.ConvertAsync(file.FullPath, targetPath, settings, cancellationToken);

        if (!conversionResult.Success)
        {
            item.Status = ConversionStatus.Failed;
            item.ErrorMessage = conversionResult.ErrorMessage ?? "Conversion failed";
            return;
        }

        item.OutputSize = conversionResult.OutputSizeBytes;

        // 5. Option: If output is larger than original, keep original
        if (settings.KeepOriginalIfLarger && item.OutputSize > file.FileSizeBytes)
        {
            _logService.LogInfo($"Output larger than original ({item.OutputSize} > {file.FileSizeBytes} bytes). Keeping original for {file.FileName}.");
            try
            {
                if (File.Exists(targetPath)) File.Delete(targetPath);
            }
            catch { }

            item.Status = ConversionStatus.Skipped;
            item.ErrorMessage = $"Output larger than original ({FormatBytes(item.OutputSize)} > {FormatBytes(file.FileSizeBytes)}). Original kept.";
            return;
        }

        // 6. Delete original if enabled and verified
        if (settings.DeleteOriginalsAfterSuccess)
        {
            bool deleted = _backupService.SafeDeleteOriginal(
                file.FullPath,
                settings.BackupOriginals,
                backupVerified,
                true,
                targetPath);

            if (!deleted)
            {
                _logService.LogWarning($"Could not delete original file {file.FullPath}");
            }
        }

        item.Status = ConversionStatus.Success;
        item.Progress = 100;
    }

    private static string GenerateUniqueFilePath(string folder, string baseName, string ext)
    {
        int index = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(folder, $"{baseName} ({index}){ext}");
            index++;
        } while (File.Exists(candidate));

        return candidate;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
    }
}
