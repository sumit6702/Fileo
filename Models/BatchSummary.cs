namespace ImageBatchConverter.Models;

public class BatchSummary
{
    public int TotalFiles { get; set; }
    public int ConvertedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }

    public long TotalOriginalBytes { get; set; }
    public long TotalConvertedBytes { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public string OutputFolder { get; set; } = string.Empty;
    public string BackupFolder { get; set; } = string.Empty;

    public long SavedBytes => TotalOriginalBytes > 0 && TotalConvertedBytes > 0
        ? TotalOriginalBytes - TotalConvertedBytes
        : 0;

    public double SavedPercent => TotalOriginalBytes > 0
        ? Math.Round(((double)SavedBytes / TotalOriginalBytes) * 100.0, 1)
        : 0.0;
}
