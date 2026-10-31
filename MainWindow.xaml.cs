using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageBatchConverter.Models;
using ImageBatchConverter.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ImageBatchConverter;

public partial class MainWindow : FluentWindow
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // Responsive window sizing: adapt to screen work area (laptop, display scaling, taskbar)
        AdjustWindowSizeToScreen();

        // Apply system theme matching
        SystemThemeWatcher.Watch(this);
    }

    private void AdjustWindowSizeToScreen()
    {
        double workHeight = SystemParameters.WorkArea.Height;
        double workWidth = SystemParameters.WorkArea.Width;

        // If default height is taller than 86% of work area, resize down
        if (Height > workHeight * 0.88 || Height > 700)
        {
            Height = Math.Max(520, workHeight * 0.85);
        }

        if (Width > workWidth * 0.92)
        {
            Width = Math.Max(880, workWidth * 0.90);
        }

        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                await ViewModel.HandleDroppedPathsAsync(files);
            }
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        var current = ApplicationThemeManager.GetAppTheme();
        if (current == ApplicationTheme.Dark)
        {
            ApplicationThemeManager.Apply(ApplicationTheme.Light);
        }
        else
        {
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        }
    }

    private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && row.Item is ConversionItem item)
        {
            ViewModel.ShowItemError(item);
        }
    }
}
