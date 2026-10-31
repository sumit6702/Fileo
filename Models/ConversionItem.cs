using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageBatchConverter.Models;

public partial class ConversionItem : ObservableObject
{
    public required string Id { get; set; }
    public required string InputPath { get; set; }
    public required string RelativePath { get; set; }
    public required string FileName { get; set; }
    public required string InputFormat { get; set; }
    public required string OutputFormat { get; set; }
    public long OriginalSize { get; set; }

    [ObservableProperty]
    private long _outputSize;

    [ObservableProperty]
    private ConversionStatus _status = ConversionStatus.Pending;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _outputPath;

    [ObservableProperty]
    private string? _backupPath;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private long _durationMs;

    public long SavedBytes => OriginalSize > 0 && OutputSize > 0 ? OriginalSize - OutputSize : 0;

    public double SavedPercent => OriginalSize > 0 && OutputSize > 0
        ? Math.Round(((double)(OriginalSize - OutputSize) / OriginalSize) * 100.0, 1)
        : 0.0;

    public bool IsLargerThanOriginal => OutputSize > 0 && OutputSize > OriginalSize;

    partial void OnOutputSizeChanged(long value)
    {
        OnPropertyChanged(nameof(SavedBytes));
        OnPropertyChanged(nameof(SavedPercent));
        OnPropertyChanged(nameof(IsLargerThanOriginal));
    }
}
