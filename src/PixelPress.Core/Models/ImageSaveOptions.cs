namespace PixelPress.Core;

public enum OutputImageFormat
{
    Jpeg,
    Png,
    Webp,
    Gif,
    Bmp,
    Tiff
}

public sealed class ImageSaveOptions
{
    public OutputImageFormat? Format { get; set; }

    public int? Quality { get; set; }

    public long? TargetFileSizeBytes { get; set; }

    public bool StripMetadata { get; set; }

    public int MinQuality { get; set; } = 1;

    public int MaxQuality { get; set; } = 100;
}

public sealed record ImageSaveResult(
    long BytesWritten,
    bool TargetSatisfied = true,
    string? Message = null);
