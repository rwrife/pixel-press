using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Ai;

public sealed record CropFocusPoint(double X, double Y)
{
    public static CropFocusPoint Center { get; } = new(0.5, 0.5);

    public bool IsValid => X is >= 0 and <= 1 && Y is >= 0 and <= 1;
}

public interface IImageAiService
{
    Task<bool> IsReachableAsync(CancellationToken cancellationToken = default);

    Task<CropFocusPoint?> SuggestCropFocusAsync(
        Image<Rgba32> image,
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string?> SuggestFileNameAsync(
        Image<Rgba32> image,
        string inputPath,
        CancellationToken cancellationToken = default);
}
