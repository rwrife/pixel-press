using PixelPress.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Imaging;

public sealed class ImageSharpImageProcessor : IImageProcessor
{
    private readonly IImageCodec _codec;

    public ImageSharpImageProcessor(IImageCodec? codec = null)
    {
        _codec = codec ?? new ImageSharpCodec();
    }

    public Task<Image<Rgba32>> LoadAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        return _codec.LoadAsync(inputPath, cancellationToken);
    }

    public Task<ImageSaveResult> SaveAsync(
        Image<Rgba32> image,
        string outputPath,
        ImageSaveOptions? saveOptions = null,
        CancellationToken cancellationToken = default)
    {
        return _codec.SaveAsync(image, outputPath, saveOptions, cancellationToken);
    }
}
