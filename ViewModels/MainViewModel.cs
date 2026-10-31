using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageBatchConverter.Models;
using ImageBatchConverter.Services;
using Microsoft.Win32;

namespace ImageBatchConverter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IScanService _scanService;
    private readonly IConversionService _conversionService;
    private readonly ISettingsService _settingsService;
    private readonly IBackupService _backupService;
    private readonly ILogService _logService;

    private CancellationTokenSource? _batchCts;

    // --- INPUT SECTION ---
    [ObservableProperty]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    private bool _filterPng = true;

    [ObservableProperty]
    private bool _filterJpg = true;

    [ObservableProperty]
    private bool _filterWebP = false;

    [ObservableProperty]
    private bool _filterAvif = false;

    [ObservableProperty]
    private bool _filterHeic = false;

    [ObservableProperty]
    private bool _filterTiff = false;

    [ObservableProperty]
    private bool _filterBmp = false;

    [ObservableProperty]
    private bool _filterGif = false;

    [ObservableProperty]
    private bool _filterAllSupported = false;

    [ObservableProperty]
    private bool _skipIfAlreadyTargetFormat = true;

    [ObservableProperty]
    private ScanSummary _scanSummary = new();

    [ObservableProperty]
    private bool _isScanning = false;

    public ObservableCollection<ImageFileInfo> ScannedFiles { get; } = new();

    // --- OUTPUT SECTION ---
    [ObservableProperty]
    private OutputFormatType _selectedOutputFormat = OutputFormatType.WebP;

    [ObservableProperty]
    private ConversionSettings _settings = new();

    [ObservableProperty]
    private ObservableCollection<PresetModel> _presets = new();

    [ObservableProperty]
    private PresetModel? _selectedPreset;

    // Direct Two-Way properties for Save Location RadioButtons
    public bool SaveInSameFolder
    {
        get => Settings.SaveInSameFolder;
        set
        {
            if (Settings.SaveInSameFolder != value)
            {
                Settings.SaveInSameFolder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SaveInSeparateFolder));
            }
        }
    }

    public bool SaveInSeparateFolder
    {
        get => !Settings.SaveInSameFolder;
        set
        {
            if (Settings.SaveInSameFolder == value)
            {
                Settings.SaveInSameFolder = !value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SaveInSameFolder));

                // If user toggles to separate folder and custom folder is empty, auto-suggest Converted folder
                if (value && string.IsNullOrWhiteSpace(Settings.CustomOutputFolder) && !string.IsNullOrWhiteSpace(InputPath))
                {
                    Settings.CustomOutputFolder = Path.Combine(InputPath, "Converted");
                    OnPropertyChanged(nameof(Settings));
                }
            }
        }
    }

    // --- PROGRESS & EXECUTION SECTION ---
    [ObservableProperty]
    private bool _isRunning = false;

    [ObservableProperty]
    private bool _isPaused = false;

    [ObservableProperty]
    private double _overallProgress = 0;

    [ObservableProperty]
    private string _overallProgressText = "0 / 0 (0%)";

    [ObservableProperty]
    private double _currentFileProgress = 0;

    [ObservableProperty]
    private string _currentFileLabel = "Ready";

    [ObservableProperty]
    private string _elapsedTimeText = "00:00:00";

    [ObservableProperty]
    private string _etaText = "ETA: --:--:--";

    [ObservableProperty]
    private string _speedText = "0.0 files/s";

    [ObservableProperty]
    private int _convertedCount = 0;

    [ObservableProperty]
    private int _skippedCount = 0;

    [ObservableProperty]
    private int _failedCount = 0;

    [ObservableProperty]
    private long _totalOriginalBytes = 0;

    [ObservableProperty]
    private long _totalConvertedBytes = 0;

    [ObservableProperty]
    private long _savedBytes = 0;

    [ObservableProperty]
    private double _savedPercent = 0.0;

    [ObservableProperty]
    private bool _isSavedBytesNegative = false;

    [ObservableProperty]
    private TaskbarItemProgressState _taskbarState = TaskbarItemProgressState.None;

    [ObservableProperty]
    private double _taskbarProgress = 0;

    // --- RESULTS SECTION ---
    public ObservableCollection<ConversionItem> ConversionItems { get; } = new();

    [ObservableProperty]
    private ConversionItem? _selectedItem;

    [ObservableProperty]
    private BatchSummary? _summary;

    [ObservableProperty]
    private bool _hasResults = false;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // --- FLUENT MODAL DIALOG STATES ---
    [ObservableProperty]
    private bool _isConfirmDialogOpen = false;

    [ObservableProperty]
    private bool _isCancelDialogOpen = false;

    [ObservableProperty]
    private bool _isDetailsDialogOpen = false;

    [ObservableProperty]
    private bool _isNotificationDialogOpen = false;

    [ObservableProperty]
    private string _notificationTitle = string.Empty;

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    [ObservableProperty]
    private ConversionItem? _dialogItemDetails;

    // --- SMART WARNINGS ---
    public bool ShowTransparencyWarning =>
        (SelectedOutputFormat == OutputFormatType.JPEG || SelectedOutputFormat == OutputFormatType.BMP)
        && ScannedFiles.Any(f => f.HasTransparency);

    public bool ShowLossyToLosslessWarning =>
        SelectedOutputFormat == OutputFormatType.PNG
        && ScannedFiles.Any(f => f.FormatName == "JPG");

    public bool ShowAnimatedGifWarning =>
        ScannedFiles.Any(f => f.IsAnimatedGif)
        && SelectedOutputFormat != OutputFormatType.WebP
        && SelectedOutputFormat != OutputFormatType.GIF;

    public bool ShowSlowCodecWarning =>
        SelectedOutputFormat == OutputFormatType.AVIF
        || SelectedOutputFormat == OutputFormatType.JPEGXL;

    public MainViewModel(
        IScanService scanService,
        IConversionService conversionService,
        ISettingsService settingsService,
        IBackupService backupService,
        ILogService logService)
    {
        _scanService = scanService;
        _conversionService = conversionService;
        _settingsService = settingsService;
        _backupService = backupService;
        _logService = logService;

        _conversionService.ItemStarted += OnItemStarted;
        _conversionService.ItemCompleted += OnItemCompleted;
    }

    public async Task InitializeAsync()
    {
        Settings = await _settingsService.LoadSettingsAsync();
        SelectedOutputFormat = Settings.TargetFormat;
        OnPropertyChanged(nameof(SaveInSameFolder));
        OnPropertyChanged(nameof(SaveInSeparateFolder));

        var presets = await _settingsService.GetPresetsAsync();
        Presets.Clear();
        foreach (var p in presets) Presets.Add(p);

        SelectedPreset = Presets.FirstOrDefault();
    }

    partial void OnSelectedOutputFormatChanged(OutputFormatType value)
    {
        Settings.TargetFormat = value;
        UpdateSmartWarnings();
        _ = TriggerScanAsync();
    }

    partial void OnIncludeSubfoldersChanged(bool value) => _ = TriggerScanAsync();
    partial void OnFilterPngChanged(bool value) => _ = TriggerScanAsync();
    partial void OnFilterJpgChanged(bool value) => _ = TriggerScanAsync();
    partial void OnFilterWebPChanged(bool value) => _ = TriggerScanAsync();
    partial void OnFilterAvifChanged(bool value) => _ = TriggerScanAsync();
    partial void OnFilterHeicChanged(bool value) => _ = TriggerScanAsync();
    partial void OnFilterTiffChanged(bool value) => _ = TriggerScanAsync();
    partial void OnFilterBmpChanged(bool value) => _ = TriggerScanAsync();
    partial void OnFilterGifChanged(bool value) => _ = TriggerScanAsync();
    partial void OnFilterAllSupportedChanged(bool value) => _ = TriggerScanAsync();
    partial void OnSkipIfAlreadyTargetFormatChanged(bool value)
    {
        Settings.SkipIfAlreadyTargetFormat = value;
        _ = TriggerScanAsync();
    }

    private void UpdateSmartWarnings()
    {
        OnPropertyChanged(nameof(ShowTransparencyWarning));
        OnPropertyChanged(nameof(ShowLossyToLosslessWarning));
        OnPropertyChanged(nameof(ShowAnimatedGifWarning));
        OnPropertyChanged(nameof(ShowSlowCodecWarning));
    }

    [RelayCommand]
    public async Task BrowseInputFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Image Folder - Fileo",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            InputPath = dialog.FolderName;

            // If user has separate output folder selected and path is blank or defaults, suggest a clean path
            if (SaveInSeparateFolder && string.IsNullOrWhiteSpace(Settings.CustomOutputFolder))
            {
                Settings.CustomOutputFolder = Path.Combine(InputPath, "Converted");
                OnPropertyChanged(nameof(Settings));
            }

            await TriggerScanAsync();
        }
    }

    [RelayCommand]
    public void BrowseCustomOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Folder - Fileo"
        };

        if (dialog.ShowDialog() == true)
        {
            Settings.CustomOutputFolder = dialog.FolderName;
            SaveInSeparateFolder = true;
            OnPropertyChanged(nameof(Settings));
        }
    }

    [RelayCommand]
    public void BrowseBackupFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Backup Folder - Fileo"
        };

        if (dialog.ShowDialog() == true)
        {
            Settings.BackupFolder = dialog.FolderName;
            OnPropertyChanged(nameof(Settings));
        }
    }

    public async Task HandleDroppedPathsAsync(string[] paths)
    {
        if (paths == null || paths.Length == 0) return;

        if (paths.Length == 1 && Directory.Exists(paths[0]))
        {
            InputPath = paths[0];
            if (SaveInSeparateFolder && string.IsNullOrWhiteSpace(Settings.CustomOutputFolder))
            {
                Settings.CustomOutputFolder = Path.Combine(InputPath, "Converted");
                OnPropertyChanged(nameof(Settings));
            }
            await TriggerScanAsync();
        }
        else
        {
            InputPath = $"{paths.Length} items dropped";
            await ScanExplicitPathsAsync(paths);
        }
    }

    [RelayCommand]
    public async Task TriggerScanAsync()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || !Directory.Exists(InputPath))
            return;

        await ScanExplicitPathsAsync(new[] { InputPath });
    }

    private async Task ScanExplicitPathsAsync(IEnumerable<string> paths)
    {
        if (IsScanning) return;

        IsScanning = true;
        StatusMessage = "Scanning files...";

        try
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (FilterPng) allowed.Add("PNG");
            if (FilterJpg) allowed.Add("JPG");
            if (FilterWebP) allowed.Add("WebP");
            if (FilterAvif) allowed.Add("AVIF");
            if (FilterHeic) allowed.Add("HEIC");
            if (FilterTiff) allowed.Add("TIFF");
            if (FilterBmp) allowed.Add("BMP");
            if (FilterGif) allowed.Add("GIF");

            var (files, summary) = await _scanService.ScanPathsAsync(
                paths,
                IncludeSubfolders,
                allowed,
                FilterAllSupported,
                SkipIfAlreadyTargetFormat,
                SelectedOutputFormat);

            ScannedFiles.Clear();
            foreach (var f in files) ScannedFiles.Add(f);

            ScanSummary = summary;
            UpdateSmartWarnings();
            StatusMessage = $"Found {summary.TotalFiles} images ({summary.TotalSizeFormatted}).";
        }
        catch (Exception ex)
        {
            _logService.LogError("Scan error", ex);
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    // Opens the Fluent Confirmation Dialog
    [RelayCommand]
    public void StartBatch()
    {
        if (ScannedFiles.Count == 0)
        {
            ShowNotification("No Images Found", "Please choose a folder or drag and drop images to convert.");
            return;
        }

        if (SaveInSeparateFolder && string.IsNullOrWhiteSpace(Settings.CustomOutputFolder))
        {
            if (!string.IsNullOrWhiteSpace(InputPath) && Directory.Exists(InputPath))
            {
                Settings.CustomOutputFolder = Path.Combine(InputPath, "Converted");
                OnPropertyChanged(nameof(Settings));
            }
            else
            {
                ShowNotification("Output Folder Required", "Please choose a separate output folder for converted images.");
                return;
            }
        }

        // Open Fluent Confirmation Dialog
        IsConfirmDialogOpen = true;
    }

    [RelayCommand]
    public void CloseConfirmDialog()
    {
        IsConfirmDialogOpen = false;
    }

    // Executes the actual conversion batch from the Fluent Confirmation Dialog
    [RelayCommand]
    public async Task ConfirmAndStartBatchAsync()
    {
        IsConfirmDialogOpen = false;

        // Reset progress & queue
        IsRunning = true;
        IsPaused = false;
        HasResults = false;
        OverallProgress = 0;
        CurrentFileProgress = 0;
        ConvertedCount = 0;
        SkippedCount = 0;
        FailedCount = 0;
        TotalOriginalBytes = 0;
        TotalConvertedBytes = 0;
        SavedBytes = 0;
        SavedPercent = 0.0;
        IsSavedBytesNegative = false;
        ConversionItems.Clear();
        TaskbarState = TaskbarItemProgressState.Normal;
        TaskbarProgress = 0;

        _batchCts = new CancellationTokenSource();

        var progressReporter = new Progress<BatchProgressState>(state =>
        {
            OverallProgress = state.OverallPercentage;
            OverallProgressText = $"{state.ProcessedFiles} / {state.TotalFiles} ({state.OverallPercentage:F1}%)";
            ElapsedTimeText = state.ElapsedTime.ToString(@"hh\:mm\:ss");
            EtaText = $"ETA: {state.EstimatedTimeRemaining:hh\\:mm\\:ss}";
            SpeedText = $"{state.FilesPerSecond:F1} files/s";
            ConvertedCount = state.ConvertedCount;
            SkippedCount = state.SkippedCount;
            FailedCount = state.FailedCount;
            TotalOriginalBytes = state.TotalOriginalBytes;
            TotalConvertedBytes = state.TotalConvertedBytes;

            long saved = state.TotalOriginalBytes - state.TotalConvertedBytes;
            SavedBytes = saved;
            IsSavedBytesNegative = saved < 0;
            SavedPercent = state.TotalOriginalBytes > 0
                ? Math.Round(((double)saved / state.TotalOriginalBytes) * 100.0, 1)
                : 0.0;

            TaskbarProgress = Math.Clamp(state.OverallPercentage / 100.0, 0, 1);
            if (state.FailedCount > 0) TaskbarState = TaskbarItemProgressState.Error;
        });

        try
        {
            Summary = await _conversionService.StartBatchAsync(
                ScannedFiles,
                InputPath,
                Settings,
                progressReporter,
                _batchCts.Token);

            HasResults = true;
            TaskbarState = Summary.FailedCount > 0 ? TaskbarItemProgressState.Error : TaskbarItemProgressState.None;
            StatusMessage = $"Batch complete! Converted: {Summary.ConvertedCount}, Skipped: {Summary.SkippedCount}, Failed: {Summary.FailedCount}.";

            _logService.ShowToastNotification(
                "Fileo - Conversion Completed",
                $"Successfully processed {Summary.TotalFiles} images. Saved {ScanSummary.FormatBytes(Summary.SavedBytes)} ({Summary.SavedPercent}%).");

            await _settingsService.SaveSettingsAsync(Settings);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Conversion canceled by user.";
            TaskbarState = TaskbarItemProgressState.Paused;
            ShowNotification("Conversion Canceled", "Batch conversion stopped safely. No partial files were retained.");
        }
        catch (Exception ex)
        {
            _logService.LogError("Batch error", ex);
            StatusMessage = $"Error: {ex.Message}";
            TaskbarState = TaskbarItemProgressState.Error;
            ShowNotification("Batch Error", $"Batch conversion encountered an error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            IsPaused = false;
        }
    }

    [RelayCommand]
    public void TogglePauseResume()
    {
        if (!IsRunning) return;

        if (IsPaused)
        {
            _conversionService.Resume();
            IsPaused = false;
            TaskbarState = TaskbarItemProgressState.Normal;
        }
        else
        {
            _conversionService.Pause();
            IsPaused = true;
            TaskbarState = TaskbarItemProgressState.Paused;
        }
    }

    [RelayCommand]
    public void PromptCancelBatch()
    {
        if (!IsRunning) return;
        IsCancelDialogOpen = true;
    }

    [RelayCommand]
    public void ConfirmCancelBatch()
    {
        IsCancelDialogOpen = false;
        _batchCts?.Cancel();
    }

    [RelayCommand]
    public void CloseCancelDialog()
    {
        IsCancelDialogOpen = false;
    }

    [RelayCommand]
    public void ApplyPreset(PresetModel? preset)
    {
        if (preset == null) return;

        Settings = preset.Settings.Clone();
        SelectedOutputFormat = Settings.TargetFormat;
        OnPropertyChanged(nameof(SaveInSameFolder));
        OnPropertyChanged(nameof(SaveInSeparateFolder));
        UpdateSmartWarnings();
        StatusMessage = $"Applied preset: {preset.Name}";
    }

    [RelayCommand]
    public async Task SaveCurrentAsPresetAsync()
    {
        string name = $"Custom Preset {DateTime.Now:MMM dd HH:mm}";
        var newPreset = new PresetModel
        {
            Name = name,
            Description = $"Custom preset for {SelectedOutputFormat} created on {DateTime.Now:g}",
            Settings = Settings.Clone(),
            IsBuiltIn = false
        };

        await _settingsService.SavePresetAsync(newPreset);
        Presets.Add(newPreset);
        SelectedPreset = newPreset;
        StatusMessage = $"Preset '{name}' saved successfully!";
    }

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        if (ConversionItems.Count == 0)
        {
            ShowNotification("No Results", "No conversion results available to export.");
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"fileo_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (saveDialog.ShowDialog() == true)
        {
            await _logService.ExportResultsToCsvAsync(saveDialog.FileName, ConversionItems);
            ShowNotification("Report Exported", $"CSV report saved successfully to:\n{saveDialog.FileName}");
        }
    }

    [RelayCommand]
    public void OpenOutputFolder()
    {
        string folder = Settings.SaveInSameFolder
            ? InputPath
            : (!string.IsNullOrWhiteSpace(Settings.CustomOutputFolder) ? Settings.CustomOutputFolder : Path.Combine(InputPath, "Converted"));

        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        }
        else
        {
            ShowNotification("Folder Not Found", $"Output folder does not exist:\n{folder}");
        }
    }

    [RelayCommand]
    public void OpenBackupFolder()
    {
        string folder = Summary?.BackupFolder ?? Settings.BackupFolder;
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        }
        else
        {
            ShowNotification("Folder Not Found", "Backup folder does not exist or no backup was created.");
        }
    }

    public void ShowItemError(ConversionItem? item)
    {
        if (item == null) return;
        DialogItemDetails = item;
        IsDetailsDialogOpen = true;
    }

    [RelayCommand]
    public void CloseDetailsDialog()
    {
        IsDetailsDialogOpen = false;
        DialogItemDetails = null;
    }

    public void ShowNotification(string title, string message)
    {
        NotificationTitle = title;
        NotificationMessage = message;
        IsNotificationDialogOpen = true;
    }

    [RelayCommand]
    public void CloseNotificationDialog()
    {
        IsNotificationDialogOpen = false;
    }

    private void OnItemStarted(ConversionItem item)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ConversionItems.Add(item);
            CurrentFileLabel = $"{item.FileName} -> {item.OutputFormat}";
            CurrentFileProgress = 10;
        });
    }

    private void OnItemCompleted(ConversionItem item)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentFileProgress = 100;
        });
    }
}
