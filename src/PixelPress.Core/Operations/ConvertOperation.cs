using PixelPress.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Operations;

public sealed class ConvertOperation : IImageOperation
{
    public ConvertOperation(OutputImageFormat targetFormat)
    {
        TargetFormat = targetFormat;
    }

    public string Name => "convert";

    public OutputImageFormat TargetFormat { get; }

    public Task ApplyAsync(Image<Rgba32> image, ImageJobContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        context.SaveOptions.Format = TargetFormat;
        context.OutputPath = Path.ChangeExtension(context.OutputPath, TargetFormat.ToFileExtension());

        return Task.CompletedTask;
    }

    public static ConvertOperation ToJpeg() => new(OutputImageFormat.Jpeg);

    public static ConvertOperation ToPng() => new(OutputImageFormat.Png);

    public static ConvertOperation ToWebp() => new(OutputImageFormat.Webp);

    public static ConvertOperation ToGif() => new(OutputImageFormat.Gif);

    public static ConvertOperation ToBmp() => new(OutputImageFormat.Bmp);

    public static ConvertOperation ToTiff() => new(OutputImageFormat.Tiff);
}
