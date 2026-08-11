using System.Collections.Concurrent;
using System.Diagnostics;
using PixelPress.Core.Abstractions;

namespace PixelPress.Core;

public sealed class PipelineRunner
{
    private readonly IImageProcessor _imageProcessor;

    public PipelineRunner(IImageProcessor imageProcessor)
    {
        _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
    }

    public async Task<BatchResult> RunAsync(
        PipelineRunRequest request,
        Action<PipelineProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.WriteInPlace)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputDirectory);
        }

        if (request.InputPaths.Count == 0)
        {
            return new BatchResult([], TimeSpan.Zero);
        }

        if (!request.WriteInPlace)
        {
            Directory.CreateDirectory(request.OutputDirectory);
        }

        var inputs = request.InputPaths.ToArray();
        var total = inputs.Length;
        var completed = 0;
        var results = new ConcurrentBag<FileProcessResult>();
        var reservedOutputPaths = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var batchStopwatch = Stopwatch.StartNew();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = request.MaxDegreeOfParallelism <= 0
                ? Environment.ProcessorCount
                : request.MaxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        try
        {
            await Parallel.ForEachAsync(
                inputs.Select((path, index) => (path, index)),
                parallelOptions,
                async (item, token) =>
                {
                    var fileStopwatch = Stopwatch.StartNew();
                    var inputPath = item.path;
                    var outputPath = request.WriteInPlace
                        ? inputPath
                        : Path.Combine(request.OutputDirectory, Path.GetFileName(inputPath));
                    var inputBytes = TryGetFileLength(inputPath);
                    var outputBytes = 0L;
                    var status = FileProcessStatus.Success;
                    string? errorMessage = null;
                    string? backupPath = null;

                    try
                    {
                        token.ThrowIfCancellationRequested();

                        using var image = await _imageProcessor.LoadAsync(inputPath, token);
                        var context = new ImageJobContext(inputPath, outputPath, item.index, total);

                        foreach (var operation in request.Operations)
                        {
                            token.ThrowIfCancellationRequested();
                            await operation.ApplyAsync(image, context, token);
                        }

                        outputPath = context.OutputPath;
                        outputPath = ReserveOutputPath(
                            inputPath,
                            outputPath,
                            reservedOutputPaths,
                            request.WriteInPlace,
                            request.OverwriteExisting);
                        context.OutputPath = outputPath;

                        if (!request.OverwriteExisting && !PathsEqual(outputPath, inputPath) && File.Exists(outputPath))
                        {
                            status = FileProcessStatus.Skipped;
                            outputBytes = TryGetFileLength(outputPath);
                            return;
                        }

                        if (request.WriteInPlace && request.CreateBackupWhenInPlace && File.Exists(inputPath))
                        {
                            backupPath = CreateBackupPath(inputPath);
                            File.Copy(inputPath, backupPath, overwrite: false);
                        }

                        var saveResult = await _imageProcessor.SaveAsync(image, outputPath, context.SaveOptions, token);
                        outputBytes = saveResult.BytesWritten > 0 ? saveResult.BytesWritten : TryGetFileLength(outputPath);

                        if (!saveResult.TargetSatisfied)
                        {
                            status = FileProcessStatus.Error;
                            errorMessage = saveResult.Message ?? "Target file size could not be met.";
                            RestoreOriginalIfNeeded(request.WriteInPlace, inputPath, outputPath, backupPath);
                            outputBytes = TryGetFileLength(outputPath);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        status = FileProcessStatus.Canceled;
                        errorMessage = "Operation canceled.";
                        RestoreOriginalIfNeeded(request.WriteInPlace, inputPath, outputPath, backupPath);
                    }
                    catch (Exception ex)
                    {
                        status = FileProcessStatus.Error;
                        errorMessage = ex.Message;
                        RestoreOriginalIfNeeded(request.WriteInPlace, inputPath, outputPath, backupPath);
                        outputBytes = TryGetFileLength(outputPath);
                    }
                    finally
                    {
                        fileStopwatch.Stop();

                        results.Add(
                            new FileProcessResult(
                                inputPath,
                                outputPath,
                                status,
                                inputBytes,
                                outputBytes,
                                fileStopwatch.Elapsed,
                                errorMessage));

                        var finished = Interlocked.Increment(ref completed);
                        onProgress?.Invoke(new PipelineProgress(finished, total, inputPath, status, errorMessage));
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Return partial results for files that completed or were canceled.
        }

        batchStopwatch.Stop();

        var ordered = results
            .OrderBy(r => r.InputPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new BatchResult(ordered, batchStopwatch.Elapsed);
    }

    private static string ReserveOutputPath(
        string inputPath,
        string proposedOutputPath,
        ConcurrentDictionary<string, byte> reservedOutputPaths,
        bool writeInPlace,
        bool overwriteExisting)
    {
        if (writeInPlace && PathsEqual(inputPath, proposedOutputPath))
        {
            return proposedOutputPath;
        }

        var directory = Path.GetDirectoryName(proposedOutputPath) ?? Directory.GetCurrentDirectory();
        var baseName = Path.GetFileNameWithoutExtension(proposedOutputPath);
        var extension = Path.GetExtension(proposedOutputPath);

        var suffix = 0;

        while (true)
        {
            var candidate = suffix == 0
                ? Path.Combine(directory, $"{baseName}{extension}")
                : Path.Combine(directory, $"{baseName}-{suffix}{extension}");

            var fullPath = Path.GetFullPath(candidate);
            if (!reservedOutputPaths.TryAdd(fullPath, 0))
            {
                suffix++;
                continue;
            }

            if (!overwriteExisting && File.Exists(candidate))
            {
                reservedOutputPaths.TryRemove(fullPath, out _);
                suffix++;
                continue;
            }

            return candidate;
        }
    }

    private static void RestoreOriginalIfNeeded(bool writeInPlace, string inputPath, string outputPath, string? backupPath)
    {
        if (!writeInPlace || string.IsNullOrWhiteSpace(backupPath) || !PathsEqual(inputPath, outputPath))
        {
            return;
        }

        try
        {
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, inputPath, overwrite: true);
            }
        }
        catch
        {
            // Best effort restore only.
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateBackupPath(string inputPath)
    {
        var preferred = $"{inputPath}.bak";
        if (!File.Exists(preferred))
        {
            return preferred;
        }

        for (var i = 1; ; i++)
        {
            var candidate = $"{inputPath}.bak{i}";
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static long TryGetFileLength(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }
        catch
        {
            return 0L;
        }
    }
}
