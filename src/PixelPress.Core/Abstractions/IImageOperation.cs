using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Abstractions;

public interface IImageOperation
{
    string Name { get; }

    Task ApplyAsync(Image<Rgba32> image, ImageJobContext context, CancellationToken cancellationToken = default);
}
