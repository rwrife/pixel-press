using PixelPress.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Operations;

public sealed class CompressOperation : IImageOperation
{
    private CompressOperation(int? quality, long? targetFileSizeBytes, bool stripMetadata, int minQuality, int maxQuality)
    {
        if (quality is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 0 and 100.");
        }

        if (targetFileSizeBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetFileSizeBytes), "Target file size must be greater than zero.");
        }

        if (minQuality is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minQuality), "Min quality must be between 0 and 100.");
        }

        if (maxQuality is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxQuality), "Max quality must be between 0 and 100.");
        }

        if (minQuality > maxQuality)
        {
            throw new ArgumentException("Min quality cannot be greater than max quality.");
        }

        Quality = quality;
        TargetFileSizeBytes = targetFileSizeBytes;
        StripMetadata = stripMetadata;
        MinQuality = minQuality;
        MaxQuality = maxQuality;
    }

    public string Name => "compress";

    public int? Quality { get; }

    public long? TargetFileSizeBytes { get; }

    public bool StripMetadata { get; }

    public int MinQuality { get; }

    public int MaxQuality { get; }

    public static CompressOperation QualityOnly(int quality, bool stripMetadata = false)
        => new(quality, null, stripMetadata, minQuality: 0, maxQuality: 100);

    public static CompressOperation TargetFileSize(long targetBytes, bool stripMetadata = false, int minQuality = 0, int maxQuality = 100)
        => new(null, targetBytes, stripMetadata, minQuality, maxQuality);

    public static CompressOperation QualityWithTarget(
        int quality,
        long targetBytes,
        bool stripMetadata = false,
        int minQuality = 0)
        => new(quality, targetBytes, stripMetadata, minQuality, quality);

    public Task ApplyAsync(Image<Rgba32> image, ImageJobContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        context.SaveOptions.Quality = Quality;
        context.SaveOptions.TargetFileSizeBytes = TargetFileSizeBytes;
        context.SaveOptions.StripMetadata = StripMetadata;
        context.SaveOptions.MinQuality = MinQuality;
        context.SaveOptions.MaxQuality = MaxQuality;

        return Task.CompletedTask;
    }
}
