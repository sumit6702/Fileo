using System.Windows;
using ImageBatchConverter.Services;
using ImageBatchConverter.Services.Converters;
using ImageBatchConverter.ViewModels;

namespace ImageBatchConverter;

public partial class App : Application
{
    public static MainViewModel? ViewModel { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logService = new LogService();
        var backupService = new BackupService(logService);
        var settingsService = new SettingsService();
        var scanService = new ScanService();

        var converters = new List<IImageConverter>
        {
            new WebPConverter(logService),
            new AvifConverter(logService),
            new JpegConverter(logService),
            new PngConverter(logService),
            new JpegXlConverter(logService),
            new TiffConverter(logService),
            new BmpConverter(logService),
            new GifConverter(logService),
            new IcoConverter(logService)
        };

        var conversionService = new ConversionService(logService, backupService, converters);

        ViewModel = new MainViewModel(
            scanService,
            conversionService,
            settingsService,
            backupService,
            logService);

        await ViewModel.InitializeAsync();

        var mainWindow = new MainWindow(ViewModel);
        mainWindow.Show();
    }
}
