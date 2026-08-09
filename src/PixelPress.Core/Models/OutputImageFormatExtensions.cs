namespace PixelPress.Core;

public static class OutputImageFormatExtensions
{
    public static string ToFileExtension(this OutputImageFormat format) => format switch
    {
        OutputImageFormat.Jpeg => ".jpg",
        OutputImageFormat.Png => ".png",
        OutputImageFormat.Webp => ".webp",
        OutputImageFormat.Gif => ".gif",
        OutputImageFormat.Bmp => ".bmp",
        OutputImageFormat.Tiff => ".tiff",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format.")
    };

    public static OutputImageFormat FromPath(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var extension = Path.GetExtension(outputPath);

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => OutputImageFormat.Jpeg,
            ".png" => OutputImageFormat.Png,
            ".webp" => OutputImageFormat.Webp,
            ".gif" => OutputImageFormat.Gif,
            ".bmp" => OutputImageFormat.Bmp,
            ".tif" or ".tiff" => OutputImageFormat.Tiff,
            _ => throw new NotSupportedException($"Output extension '{extension}' is not supported.")
        };
    }
}
