namespace ImageBatchConverter.Models;

public class ScanSummary
{
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public Dictionary<string, int> CountByFormat { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, long> SizeByFormat { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);

    public static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
