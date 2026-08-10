using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Abstractions;

public interface IImageCodec
{
    Task<Image<Rgba32>> LoadAsync(string inputPath, CancellationToken cancellationToken = default);

    Task<ImageSaveResult> SaveAsync(
        Image<Rgba32> image,
        string outputPath,
        ImageSaveOptions? saveOptions = null,
        CancellationToken cancellationToken = default);
}
