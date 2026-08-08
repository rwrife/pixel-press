using PixelPress.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Imaging;

public sealed class ImageSharpCodec : IImageCodec
{
    public Task<Image<Rgba32>> LoadAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        return Image.LoadAsync<Rgba32>(inputPath, cancellationToken);
    }

    public Task SaveAsync(Image<Rgba32> image, string outputPath, CancellationToken cancellationToken = default)
    {
        return image.SaveAsync(outputPath, cancellationToken);
    }
}
