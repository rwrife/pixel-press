using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using PixelPress.App.Commands;
using PixelPress.App.Services;
using PixelPress.Core;
using PixelPress.Core.Abstractions;
using PixelPress.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tif", ".tiff", ".heic"
    };

    private readonly AppSettingsService _settingsService = new();
    private readonly IImageProcessor _imageProcessor = new ImageSharpImageProcessor();
    private readonly PipelineRunner _pipelineRunner;

    private string _outputDirectory = string.Empty;
    private InputFileItemViewModel? _selectedInput;
    private OperationViewModel? _selectedOperation;
    private OperationKind _newOperationKind = OperationKind.Resize;
    private BitmapSource? _previewBefore;
    private BitmapSource? _previewAfter;
    private string _previewSummary = "Drop files or folders to start.";
    private bool _isRunning;
    private CancellationTokenSource? _previewCts;

    public MainWindowViewModel()
    {
        _pipelineRunner = new PipelineRunner(_imageProcessor);

        AddOperationCommand = new RelayCommand(_ => AddOperation());
        RemoveOperationCommand = new RelayCommand(
            param => RemoveOperation(param as OperationViewModel),
            param => param is OperationViewModel);
        MoveOperationUpCommand = new RelayCommand(
            param => MoveOperation(param as OperationViewModel, -1),
            param => CanMoveOperation(param as OperationViewModel, -1));
        MoveOperationDownCommand = new RelayCommand(
            param => MoveOperation(param as OperationViewModel, 1),
            param => CanMoveOperation(param as OperationViewModel, 1));
        RemoveSelectedInputCommand = new RelayCommand(
            _ => RemoveSelectedInput(),
            _ => SelectedInput is not null);
        ClearInputsCommand = new RelayCommand(
            _ => ClearInputs(),
            _ => InputFiles.Count > 0);

        Operations.CollectionChanged += OnOperationsCollectionChanged;
        LoadSettings();
    }

    public ObservableCollection<InputFileItemViewModel> InputFiles { get; } = [];

    public ObservableCollection<OperationViewModel> Operations { get; } = [];

    public IReadOnlyList<OperationKind> OperationKinds => OperationViewModel.AllKinds;

    public RelayCommand AddOperationCommand { get; }

    public RelayCommand RemoveOperationCommand { get; }

    public RelayCommand MoveOperationUpCommand { get; }

    public RelayCommand MoveOperationDownCommand { get; }

    public RelayCommand RemoveSelectedInputCommand { get; }

    public RelayCommand ClearInputsCommand { get; }

    public OperationKind NewOperationKind
    {
        get => _newOperationKind;
        set => SetProperty(ref _newOperationKind, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            if (SetProperty(ref _outputDirectory, value))
            {
                PersistSettings();
            }
        }
    }

    public InputFileItemViewModel? SelectedInput
    {
        get => _selectedInput;
        set
        {
            if (SetProperty(ref _selectedInput, value))
            {
                RemoveSelectedInputCommand.RaiseCanExecuteChanged();
                QueuePreviewRefresh();
            }
        }
    }

    public OperationViewModel? SelectedOperation
    {
        get => _selectedOperation;
        set => SetProperty(ref _selectedOperation, value);
    }

    public BitmapSource? PreviewBefore
    {
        get => _previewBefore;
        private set => SetProperty(ref _previewBefore, value);
    }

    public BitmapSource? PreviewAfter
    {
        get => _previewAfter;
        private set => SetProperty(ref _previewAfter, value);
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        private set => SetProperty(ref _previewSummary, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanRunBatch));
            }
        }
    }

    public bool CanRunBatch => !IsRunning && InputFiles.Count > 0;

    public void AddInputs(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var existing = new HashSet<string>(InputFiles.Select(i => Path.GetFullPath(i.FilePath)), StringComparer.OrdinalIgnoreCase);
        var discovered = new List<string>();

        foreach (var path in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                if (IsSupportedImageFile(fullPath))
                {
                    discovered.Add(fullPath);
                }

                continue;
            }

            if (Directory.Exists(fullPath))
            {
                foreach (var file in EnumerateImageFiles(fullPath))
                {
                    discovered.Add(file);
                }
            }
        }

        foreach (var file in discovered.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!existing.Add(file))
            {
                continue;
            }

            InputFiles.Add(InputFileItemViewModel.Create(file));
        }

        if (SelectedInput is null)
        {
            SelectedInput = InputFiles.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory) && InputFiles.Count > 0)
        {
            var firstDir = Path.GetDirectoryName(InputFiles[0].FilePath) ?? Directory.GetCurrentDirectory();
            OutputDirectory = Path.Combine(firstDir, "pixelpress-output");
        }

        ClearInputsCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanRunBatch));
        QueuePreviewRefresh();
    }

    public void RemoveSelectedInput()
    {
        if (SelectedInput is null)
        {
            return;
        }

        var index = InputFiles.IndexOf(SelectedInput);
        InputFiles.Remove(SelectedInput);

        SelectedInput = InputFiles.Count == 0
            ? null
            : InputFiles[Math.Clamp(index, 0, InputFiles.Count - 1)];

        ClearInputsCommand.RaiseCanExecuteChanged();
        RemoveSelectedInputCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanRunBatch));
        QueuePreviewRefresh();
    }

    public void ClearInputs()
    {
        InputFiles.Clear();
        SelectedInput = null;
        PreviewBefore = null;
        PreviewAfter = null;
        PreviewSummary = "Drop files or folders to start.";

        ClearInputsCommand.RaiseCanExecuteChanged();
        RemoveSelectedInputCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanRunBatch));
    }

    public async Task<BatchResult> RunBatchAsync(Action<PipelineProgress>? onProgress, CancellationToken cancellationToken)
    {
        if (InputFiles.Count == 0)
        {
            throw new InvalidOperationException("Add at least one image before running the pipeline.");
        }

        var outputDirectory = string.IsNullOrWhiteSpace(OutputDirectory)
            ? Path.Combine(Path.GetDirectoryName(InputFiles[0].FilePath) ?? Directory.GetCurrentDirectory(), "pixelpress-output")
            : OutputDirectory;

        Directory.CreateDirectory(outputDirectory);

        var request = new PipelineRunRequest
        {
            InputPaths = InputFiles.Select(i => i.FilePath).ToArray(),
            OutputDirectory = outputDirectory,
            Operations = BuildOperations(),
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
            OverwriteExisting = true,
            WriteInPlace = false,
            CreateBackupWhenInPlace = false
        };

        IsRunning = true;
        try
        {
            var result = await _pipelineRunner.RunAsync(request, onProgress, cancellationToken);
            PersistSettings();
            return result;
        }
        finally
        {
            IsRunning = false;
            OnPropertyChanged(nameof(CanRunBatch));
        }
    }

    public string BuildResultSummary(BatchResult result)
    {
        var inputBytes = result.Files.Sum(f => f.InputBytes);
        var outputBytes = result.Files.Where(f => f.Status == FileProcessStatus.Success).Sum(f => f.OutputBytes);
        var bytesSaved = inputBytes - outputBytes;

        var report = $"Processed: {result.TotalFiles}\n" +
                     $"Succeeded: {result.Succeeded}\n" +
                     $"Skipped: {result.Skipped}\n" +
                     $"Failed: {result.Failed}\n" +
                     $"Elapsed: {result.Elapsed:g}\n" +
                     $"Input size: {FormatBytes(inputBytes)}\n" +
                     $"Output size: {FormatBytes(outputBytes)}\n" +
                     $"Net size change: {FormatDeltaBytes(bytesSaved)}";

        return report;
    }

    public void PersistSettings()
    {
        var settings = new AppSettings
        {
            OutputDirectory = OutputDirectory,
            Operations = Operations.Select(o => o.ToSettings()).ToList()
        };

        _settingsService.Save(settings);
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();

        OutputDirectory = settings.OutputDirectory ?? string.Empty;

        if (settings.Operations.Count == 0)
        {
            Operations.Add(new OperationViewModel(OperationKind.Resize));
            Operations.Add(new OperationViewModel(OperationKind.Convert));
            Operations.Add(new OperationViewModel(OperationKind.Compress));
        }
        else
        {
            foreach (var operation in settings.Operations)
            {
                Operations.Add(OperationViewModel.FromSettings(operation));
            }
        }

        SelectedOperation = Operations.FirstOrDefault();
        RaiseOperationCommandState();
    }

    private void AddOperation()
    {
        var operation = new OperationViewModel(NewOperationKind);
        Operations.Add(operation);
        SelectedOperation = operation;
    }

    private void RemoveOperation(OperationViewModel? operation)
    {
        if (operation is null)
        {
            return;
        }

        var index = Operations.IndexOf(operation);
        if (index < 0)
        {
            return;
        }

        Operations.RemoveAt(index);

        if (Operations.Count > 0)
        {
            SelectedOperation = Operations[Math.Clamp(index, 0, Operations.Count - 1)];
        }

        RaiseOperationCommandState();
    }

    private bool CanMoveOperation(OperationViewModel? operation, int delta)
    {
        if (operation is null)
        {
            return false;
        }

        var index = Operations.IndexOf(operation);
        if (index < 0)
        {
            return false;
        }

        var newIndex = index + delta;
        return newIndex >= 0 && newIndex < Operations.Count;
    }

    private void MoveOperation(OperationViewModel? operation, int delta)
    {
        if (!CanMoveOperation(operation, delta) || operation is null)
        {
            return;
        }

        var oldIndex = Operations.IndexOf(operation);
        var newIndex = oldIndex + delta;
        Operations.Move(oldIndex, newIndex);
        SelectedOperation = operation;
        RaiseOperationCommandState();
    }

    private void OnOperationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<OperationViewModel>())
            {
                oldItem.PropertyChanged -= OnOperationPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<OperationViewModel>())
            {
                newItem.PropertyChanged += OnOperationPropertyChanged;
            }
        }

        RaiseOperationCommandState();
        PersistSettings();
        QueuePreviewRefresh();
    }

    private void OnOperationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        PersistSettings();
        QueuePreviewRefresh();
    }

    private void RaiseOperationCommandState()
    {
        RemoveOperationCommand.RaiseCanExecuteChanged();
        MoveOperationUpCommand.RaiseCanExecuteChanged();
        MoveOperationDownCommand.RaiseCanExecuteChanged();
    }

    private IReadOnlyList<IImageOperation> BuildOperations()
    {
        var operations = new List<IImageOperation>();

        foreach (var vm in Operations)
        {
            var operation = vm.BuildOperation();
            if (operation is not null)
            {
                operations.Add(operation);
            }
        }

        return operations;
    }

    private void QueuePreviewRefresh()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();

        var cts = new CancellationTokenSource();
        _previewCts = cts;

        _ = RefreshPreviewAsync(cts.Token);
    }

    private async Task RefreshPreviewAsync(CancellationToken cancellationToken)
    {
        if (SelectedInput is null)
        {
            PreviewBefore = null;
            PreviewAfter = null;
            PreviewSummary = "Select an image to preview.";
            return;
        }

        var inputPath = SelectedInput.FilePath;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var original = await _imageProcessor.LoadAsync(inputPath, cancellationToken);
            using var transformed = original.Clone();

            var previewOutputPath = Path.Combine(
                Path.GetTempPath(),
                $"pixelpress-preview-{Guid.NewGuid():N}{Path.GetExtension(inputPath)}");
            var context = new ImageJobContext(inputPath, previewOutputPath, index: 0, total: 1);

            foreach (var operation in BuildOperations())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await operation.ApplyAsync(transformed, context, cancellationToken);
            }

            var estimatedBytes = await EstimateOutputSizeAsync(transformed, context, cancellationToken);
            var inputBytes = TryGetFileLength(inputPath);

            PreviewBefore = ToBitmapSource(original);
            PreviewAfter = ToBitmapSource(transformed);
            PreviewSummary =
                $"{transformed.Width}×{transformed.Height} • est {FormatBytes(estimatedBytes)} (from {FormatBytes(inputBytes)})";
        }
        catch (OperationCanceledException)
        {
            // Ignore stale preview jobs.
        }
        catch (Exception ex)
        {
            PreviewBefore = null;
            PreviewAfter = null;
            PreviewSummary = $"Preview unavailable: {ex.Message}";
        }
    }

    private async Task<long> EstimateOutputSizeAsync(
        Image<Rgba32> image,
        ImageJobContext context,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(context.OutputPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"pixelpress-estimate-{Guid.NewGuid():N}{extension}");

        try
        {
            var saveResult = await _imageProcessor.SaveAsync(image, tempPath, context.SaveOptions, cancellationToken);
            if (saveResult.BytesWritten > 0)
            {
                return saveResult.BytesWritten;
            }

            return TryGetFileLength(tempPath);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private static BitmapSource ToBitmapSource(Image<Rgba32> image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        stream.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    private static IEnumerable<string> EnumerateImageFiles(string directory)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            if (IsSupportedImageFile(file))
            {
                yield return Path.GetFullPath(file);
            }
        }
    }

    private static bool IsSupportedImageFile(string path)
    {
        var extension = Path.GetExtension(path);
        return SupportedImageExtensions.Contains(extension);
    }

    private static long TryGetFileLength(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatBytes(long bytes)
    {
        var value = Math.Abs((double)bytes);
        var units = new[] { "B", "KB", "MB", "GB" };
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static string FormatDeltaBytes(long bytesSaved)
    {
        if (bytesSaved > 0)
        {
            return $"saved {FormatBytes(bytesSaved)}";
        }

        if (bytesSaved < 0)
        {
            return $"grew by {FormatBytes(-bytesSaved)}";
        }

        return "no change";
    }
}
