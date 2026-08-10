using PixelPress.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PixelPress.Core.Imaging;

public sealed class ImageSharpCodec : IImageCodec
{
    public Task<Image<Rgba32>> LoadAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        return Image.LoadAsync<Rgba32>(inputPath, cancellationToken);
    }

    public async Task<ImageSaveResult> SaveAsync(
        Image<Rgba32> image,
        string outputPath,
        ImageSaveOptions? saveOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var options = saveOptions ?? new ImageSaveOptions();
        var format = options.Format ?? OutputImageFormatExtensions.FromPath(outputPath);

        if (options.TargetFileSizeBytes is { } targetBytes && targetBytes > 0)
        {
            return await SaveWithTargetFileSizeAsync(image, outputPath, format, options, targetBytes, cancellationToken);
        }

        PrepareMetadata(image, options.StripMetadata);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
        await using var output = File.Create(outputPath);
        await image.SaveAsync(output, CreateEncoder(format, options.Quality), cancellationToken);

        return new ImageSaveResult(output.Length);
    }

    private static async Task<ImageSaveResult> SaveWithTargetFileSizeAsync(
        Image<Rgba32> image,
        string outputPath,
        OutputImageFormat format,
        ImageSaveOptions options,
        long targetBytes,
        CancellationToken cancellationToken)
    {
        if (!SupportsQualitySearch(format))
        {
            PrepareMetadata(image, options.StripMetadata);

            var encoded = await EncodeToBytesAsync(image, format, options.Quality, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
            await File.WriteAllBytesAsync(outputPath, encoded, cancellationToken);

            var satisfied = encoded.LongLength <= targetBytes;
            var message = satisfied
                ? null
                : $"Target file size {targetBytes} bytes could not be met for format {format}. Actual: {encoded.LongLength} bytes.";

            return new ImageSaveResult(encoded.LongLength, satisfied, message);
        }

        var minQuality = ClampQuality(options.MinQuality);
        var maxQuality = ClampQuality(options.Quality ?? options.MaxQuality);

        if (minQuality > maxQuality)
        {
            throw new ArgumentException($"MinQuality ({minQuality}) cannot be greater than MaxQuality ({maxQuality}).");
        }

        PrepareMetadata(image, options.StripMetadata);

        byte[]? bestPayload = null;
        var low = minQuality;
        var high = maxQuality;

        while (low <= high)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var quality = (low + high) / 2;
            var encoded = await EncodeToBytesAsync(image, format, quality, cancellationToken);

            if (encoded.LongLength <= targetBytes)
            {
                bestPayload = encoded;
                low = quality + 1;
            }
            else
            {
                high = quality - 1;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());

        if (bestPayload is null)
        {
            var fallback = await EncodeToBytesAsync(image, format, minQuality, cancellationToken);
            await File.WriteAllBytesAsync(outputPath, fallback, cancellationToken);

            return new ImageSaveResult(
                fallback.LongLength,
                targetSatisfied: false,
                message: $"Target file size {targetBytes} bytes could not be met. Minimum quality output is {fallback.LongLength} bytes.");
        }

        await File.WriteAllBytesAsync(outputPath, bestPayload, cancellationToken);
        return new ImageSaveResult(bestPayload.LongLength);
    }

    private static void PrepareMetadata(Image<Rgba32> image, bool stripMetadata)
    {
        if (!stripMetadata)
        {
            return;
        }

        image.Mutate(x => x.AutoOrient());
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
    }

    private static bool SupportsQualitySearch(OutputImageFormat format) =>
        format is OutputImageFormat.Jpeg or OutputImageFormat.Webp;

    private static int ClampQuality(int? quality) => Math.Clamp(quality ?? 90, 0, 100);

    private static async Task<byte[]> EncodeToBytesAsync(
        Image<Rgba32> image,
        OutputImageFormat format,
        int? quality,
        CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream();
        await image.SaveAsync(memory, CreateEncoder(format, quality), cancellationToken);
        return memory.ToArray();
    }

    private static IImageEncoder CreateEncoder(OutputImageFormat format, int? quality)
    {
        var normalizedQuality = ClampQuality(quality);

        return format switch
        {
            OutputImageFormat.Jpeg => new JpegEncoder { Quality = normalizedQuality },
            OutputImageFormat.Png => new PngEncoder(),
            OutputImageFormat.Webp => new WebpEncoder { Quality = normalizedQuality },
            OutputImageFormat.Gif => new GifEncoder(),
            OutputImageFormat.Bmp => new BmpEncoder(),
            OutputImageFormat.Tiff => new TiffEncoder(),
            _ => throw new NotSupportedException($"Output format '{format}' is not supported.")
        };
    }
}
