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
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputDirectory);

        if (request.InputPaths.Count == 0)
        {
            return new BatchResult([], TimeSpan.Zero);
        }

        Directory.CreateDirectory(request.OutputDirectory);

        var inputs = request.InputPaths.ToArray();
        var total = inputs.Length;
        var completed = 0;
        var results = new ConcurrentBag<FileProcessResult>();
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
                    var outputPath = Path.Combine(request.OutputDirectory, Path.GetFileName(inputPath));
                    var inputBytes = TryGetFileLength(inputPath);
                    var outputBytes = 0L;
                    var status = FileProcessStatus.Success;
                    string? errorMessage = null;

                    try
                    {
                        token.ThrowIfCancellationRequested();

                        if (!request.OverwriteExisting && File.Exists(outputPath))
                        {
                            status = FileProcessStatus.Skipped;
                            outputBytes = TryGetFileLength(outputPath);
                            return;
                        }

                        using var image = await _imageProcessor.LoadAsync(inputPath, token);
                        var context = new ImageJobContext(inputPath, outputPath, item.index, total);

                        foreach (var operation in request.Operations)
                        {
                            token.ThrowIfCancellationRequested();
                            await operation.ApplyAsync(image, context, token);
                        }

                        await _imageProcessor.SaveAsync(image, outputPath, token);
                        outputBytes = TryGetFileLength(outputPath);
                    }
                    catch (OperationCanceledException)
                    {
                        status = FileProcessStatus.Canceled;
                        errorMessage = "Operation canceled.";
                    }
                    catch (Exception ex)
                    {
                        status = FileProcessStatus.Error;
                        errorMessage = ex.Message;
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
