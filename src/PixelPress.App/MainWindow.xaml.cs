using System.Windows;
using Microsoft.Win32;
using PixelPress.App.ViewModels;
using PixelPress.Core;
using Forms = System.Windows.Forms;

namespace PixelPress.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Closing += OnClosing;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.PersistSettings();
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Select image files",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp;*.tif;*.tiff;*.heic|All files|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.AddInputs(dialog.FileNames);
        }
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder(ViewModel.OutputDirectory);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            ViewModel.AddInputs([selectedPath]);
        }
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder(ViewModel.OutputDirectory);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            ViewModel.OutputDirectory = selectedPath;
        }
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanRunBatch)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        var progressWindow = new ProgressWindow { Owner = this };
        progressWindow.CancelRequested += (_, _) => cts.Cancel();

        Action<PipelineProgress> progressHandler = progress =>
            progressWindow.Dispatcher.Invoke(() => progressWindow.UpdateProgress(progress));

        progressWindow.Show();

        try
        {
            var result = await ViewModel.RunBatchAsync(progressHandler, cts.Token);
            MessageBox.Show(
                this,
                ViewModel.BuildResultSummary(result),
                "Batch complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(
                this,
                "Batch run canceled.",
                "Canceled",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Batch run failed: {ex.Message}",
                "Run failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            progressWindow.Close();
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        HandleDrop(e);
    }

    private void InputList_Drop(object sender, DragEventArgs e)
    {
        HandleDrop(e);
        e.Handled = true;
    }

    private void HandleDrop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            ViewModel.AddInputs(paths);
        }
    }

    private static string? SelectFolder(string? initialDirectory)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select folder"
        };

        if (Directory.Exists(initialDirectory))
        {
            dialog.SelectedPath = initialDirectory;
        }

        return dialog.ShowDialog() == Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}
