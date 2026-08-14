using System.IO;
using System.Windows;
using PixelPress.App.ViewModels;
using PixelPress.Core;
using PixelPress.Core.Recipes;
using Forms = System.Windows.Forms;

namespace PixelPress.App;

public partial class MainWindow : Window
{
    private readonly RecipeStore _recipeStore = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Closing += OnClosing;

        try
        {
            _recipeStore.EnsureBuiltInRecipes();
        }
        catch
        {
            // Best effort bootstrap only.
        }
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.PersistSettings();
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
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
            System.Windows.MessageBox.Show(
                this,
                ViewModel.BuildResultSummary(result),
                "Batch complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            System.Windows.MessageBox.Show(
                this,
                "Batch run canceled.",
                "Canceled",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
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

    private void SaveRecipeButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save recipe",
            Filter = "Recipe JSON|*.json",
            AddExtension = true,
            DefaultExt = ".json",
            InitialDirectory = _recipeStore.RecipesDirectory,
            FileName = $"{RecipeStore.Slugify($"recipe-{DateTime.Now:yyyyMMdd-HHmmss}")}.json"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var recipeName = Path.GetFileNameWithoutExtension(dialog.FileName);
            var recipe = ViewModel.ExportRecipe(recipeName);
            var savedPath = _recipeStore.Save(recipe, dialog.FileName);

            System.Windows.MessageBox.Show(
                this,
                $"Recipe saved to:\n{savedPath}",
                "Recipe saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Could not save recipe: {ex.Message}",
                "Save failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LoadRecipeButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load recipe",
            Filter = "Recipe JSON|*.json|All files|*.*",
            InitialDirectory = _recipeStore.RecipesDirectory,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        LoadRecipeFromPath(dialog.FileName);
    }

    private void LoadWebPresetButton_Click(object sender, RoutedEventArgs e)
    {
        LoadBuiltInPreset("Web export");
    }

    private void LoadEmailPresetButton_Click(object sender, RoutedEventArgs e)
    {
        LoadBuiltInPreset("Email compress");
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        HandleDrop(e);
    }

    private void InputList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        HandleDrop(e);
        e.Handled = true;
    }

    private void HandleDrop(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            ViewModel.AddInputs(paths);
        }
    }

    private void LoadBuiltInPreset(string presetName)
    {
        var preset = RecipeCatalog.FindBuiltIn(presetName);
        if (preset is null)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Preset '{presetName}' was not found.",
                "Preset unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        ViewModel.ImportRecipe(preset);
    }

    private void LoadRecipeFromPath(string path)
    {
        try
        {
            var recipe = _recipeStore.Load(path);
            ViewModel.ImportRecipe(recipe);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Could not load recipe: {ex.Message}",
                "Load failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
