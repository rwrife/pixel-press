using PixelPress.Core.Abstractions;

namespace PixelPress.Core;

public enum FileProcessStatus
{
    Success,
    Skipped,
    Error,
    Canceled
}

public sealed record FileProcessResult(
    string InputPath,
    string? OutputPath,
    FileProcessStatus Status,
    long InputBytes,
    long OutputBytes,
    TimeSpan Elapsed,
    string? ErrorMessage = null)
{
    public long ByteDelta => OutputBytes - InputBytes;
}

public sealed record BatchResult(
    IReadOnlyList<FileProcessResult> Files,
    TimeSpan Elapsed)
{
    public int TotalFiles => Files.Count;
    public int Succeeded => Files.Count(r => r.Status == FileProcessStatus.Success);
    public int Skipped => Files.Count(r => r.Status == FileProcessStatus.Skipped);
    public int Failed => Files.Count(r => r.Status == FileProcessStatus.Error);
    public int Canceled => Files.Count(r => r.Status == FileProcessStatus.Canceled);
}

public sealed record PipelineProgress(
    int Completed,
    int Total,
    string InputPath,
    FileProcessStatus Status,
    string? Message = null);

public sealed class PipelineRunRequest
{
    public required IReadOnlyList<string> InputPaths { get; init; }
    public required string OutputDirectory { get; init; }
    public IReadOnlyList<IImageOperation> Operations { get; init; } = [];
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
    public bool OverwriteExisting { get; init; } = true;
}

public sealed class ImageJobContext
{
    public ImageJobContext(string inputPath, string outputPath, int index, int total)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        InputPath = inputPath;
        OutputPath = outputPath;
        Index = index;
        Total = total;
    }

    public string InputPath { get; }

    public string OutputPath { get; set; }

    public int Index { get; }

    public int Total { get; }

    public ImageSaveOptions SaveOptions { get; } = new();
}
