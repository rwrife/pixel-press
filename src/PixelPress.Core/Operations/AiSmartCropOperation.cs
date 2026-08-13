using PixelPress.Core.Ai;
using PixelPress.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PixelPress.Core.Operations;

public sealed class AiSmartCropOperation : IImageOperation
{
    private readonly IImageAiService? _aiService;

    public AiSmartCropOperation(int width, int height, bool allowUpscale, IImageAiService? aiService)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        Width = width;
        Height = height;
        AllowUpscale = allowUpscale;
        _aiService = aiService;
    }

    public string Name => "ai-smart-crop";

    public int Width { get; }

    public int Height { get; }

    public bool AllowUpscale { get; }

    public async Task ApplyAsync(Image<Rgba32> image, ImageJobContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        image.Mutate(x => x.AutoOrient());

        var sourceWidth = image.Width;
        var sourceHeight = image.Height;

        if (!AllowUpscale && (sourceWidth < Width || sourceHeight < Height))
        {
            return;
        }

        var focus = CropFocusPoint.Center;
        if (_aiService is not null)
        {
            try
            {
                var suggested = await _aiService.SuggestCropFocusAsync(image, context.InputPath, cancellationToken);
                if (suggested?.IsValid == true)
                {
                    focus = suggested;
                }
            }
            catch
            {
                // Graceful fallback to center crop.
            }
        }

        var cropArea = CalculateCropArea(sourceWidth, sourceHeight, Width, Height, focus);

        image.Mutate(x => x
            .Crop(cropArea)
            .Resize(Width, Height, KnownResamplers.Lanczos3));
    }

    internal static Rectangle CalculateCropArea(
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        CropFocusPoint focus)
    {
        var sourceAspect = (double)sourceWidth / sourceHeight;
        var targetAspect = (double)targetWidth / targetHeight;

        int cropWidth;
        int cropHeight;

        if (sourceAspect > targetAspect)
        {
            cropHeight = sourceHeight;
            cropWidth = (int)Math.Round(cropHeight * targetAspect);
        }
        else
        {
            cropWidth = sourceWidth;
            cropHeight = (int)Math.Round(cropWidth / targetAspect);
        }

        cropWidth = Math.Clamp(cropWidth, 1, sourceWidth);
        cropHeight = Math.Clamp(cropHeight, 1, sourceHeight);

        var centerX = focus.X * sourceWidth;
        var centerY = focus.Y * sourceHeight;

        var left = (int)Math.Round(centerX - (cropWidth / 2d));
        var top = (int)Math.Round(centerY - (cropHeight / 2d));

        left = Math.Clamp(left, 0, sourceWidth - cropWidth);
        top = Math.Clamp(top, 0, sourceHeight - cropHeight);

        return new Rectangle(left, top, cropWidth, cropHeight);
    }
}
