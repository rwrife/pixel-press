using PixelPress.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PixelPress.Core.Operations;

public enum ResizeMode
{
    LongestEdge,
    ExactWxH,
    Percentage,
    Fit,
    Fill
}

public sealed class ResizeOperation : IImageOperation
{
    public string Name => "resize";

    public ResizeMode Mode { get; }
    public int? Width { get; }
    public int? Height { get; }
    public double? PercentageValue { get; }
    public bool AllowUpscale { get; }

    private ResizeOperation(ResizeMode mode, int? width, int? height, double? percentageValue, bool allowUpscale)
    {
        Mode = mode;
        Width = width;
        Height = height;
        PercentageValue = percentageValue;
        AllowUpscale = allowUpscale;
    }

    public static ResizeOperation LongestEdge(int longestEdge, bool allowUpscale = false)
    {
        if (longestEdge <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(longestEdge), "Longest edge must be greater than zero.");
        }

        return new ResizeOperation(ResizeMode.LongestEdge, longestEdge, null, null, allowUpscale);
    }

    public static ResizeOperation Exact(int width, int height, bool allowUpscale = false)
    {
        ValidatePositiveDimension(width, nameof(width));
        ValidatePositiveDimension(height, nameof(height));
        return new ResizeOperation(ResizeMode.ExactWxH, width, height, null, allowUpscale);
    }

    public static ResizeOperation Percentage(double percent, bool allowUpscale = false)
    {
        if (percent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Percentage must be greater than zero.");
        }

        return new ResizeOperation(ResizeMode.Percentage, null, null, percent, allowUpscale);
    }

    public static ResizeOperation Fit(int width, int height, bool allowUpscale = false)
    {
        ValidatePositiveDimension(width, nameof(width));
        ValidatePositiveDimension(height, nameof(height));
        return new ResizeOperation(ResizeMode.Fit, width, height, null, allowUpscale);
    }

    public static ResizeOperation Fill(int width, int height, bool allowUpscale = false)
    {
        ValidatePositiveDimension(width, nameof(width));
        ValidatePositiveDimension(height, nameof(height));
        return new ResizeOperation(ResizeMode.Fill, width, height, null, allowUpscale);
    }

    public Task ApplyAsync(Image<Rgba32> image, ImageJobContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        image.Mutate(x => x.AutoOrient());

        var sourceWidth = image.Width;
        var sourceHeight = image.Height;

        switch (Mode)
        {
            case ResizeMode.LongestEdge:
                ApplyLongestEdge(image, sourceWidth, sourceHeight);
                break;
            case ResizeMode.ExactWxH:
                ApplyExact(image, sourceWidth, sourceHeight);
                break;
            case ResizeMode.Percentage:
                ApplyPercentage(image, sourceWidth, sourceHeight);
                break;
            case ResizeMode.Fit:
                ApplyFit(image, sourceWidth, sourceHeight);
                break;
            case ResizeMode.Fill:
                ApplyFill(image, sourceWidth, sourceHeight);
                break;
            default:
                throw new InvalidOperationException($"Unsupported resize mode: {Mode}");
        }

        return Task.CompletedTask;
    }

    private void ApplyLongestEdge(Image<Rgba32> image, int sourceWidth, int sourceHeight)
    {
        var longestEdge = Width.GetValueOrDefault();
        var longestCurrentEdge = Math.Max(sourceWidth, sourceHeight);

        if (!AllowUpscale && longestCurrentEdge <= longestEdge)
        {
            return;
        }

        var scale = (double)longestEdge / longestCurrentEdge;
        var targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));

        ResizeToDimensions(image, sourceWidth, sourceHeight, targetWidth, targetHeight);
    }

    private void ApplyExact(Image<Rgba32> image, int sourceWidth, int sourceHeight)
    {
        var targetWidth = Width.GetValueOrDefault();
        var targetHeight = Height.GetValueOrDefault();

        if (!AllowUpscale && (sourceWidth < targetWidth || sourceHeight < targetHeight))
        {
            return;
        }

        if (!AllowUpscale)
        {
            targetWidth = Math.Min(targetWidth, sourceWidth);
            targetHeight = Math.Min(targetHeight, sourceHeight);
        }

        ResizeToDimensions(image, sourceWidth, sourceHeight, targetWidth, targetHeight);
    }

    private void ApplyPercentage(Image<Rgba32> image, int sourceWidth, int sourceHeight)
    {
        var percentage = PercentageValue.GetValueOrDefault();
        var scale = percentage / 100d;

        if (!AllowUpscale && scale >= 1d)
        {
            return;
        }

        var targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));

        if (!AllowUpscale)
        {
            targetWidth = Math.Min(targetWidth, sourceWidth);
            targetHeight = Math.Min(targetHeight, sourceHeight);
        }

        ResizeToDimensions(image, sourceWidth, sourceHeight, targetWidth, targetHeight);
    }

    private void ApplyFit(Image<Rgba32> image, int sourceWidth, int sourceHeight)
    {
        var targetWidth = Width.GetValueOrDefault();
        var targetHeight = Height.GetValueOrDefault();

        if (!AllowUpscale && sourceWidth <= targetWidth && sourceHeight <= targetHeight)
        {
            return;
        }

        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max,
            Size = new Size(targetWidth, targetHeight),
            Sampler = KnownResamplers.Lanczos3
        }));
    }

    private void ApplyFill(Image<Rgba32> image, int sourceWidth, int sourceHeight)
    {
        var targetWidth = Width.GetValueOrDefault();
        var targetHeight = Height.GetValueOrDefault();

        if (!AllowUpscale && (sourceWidth < targetWidth || sourceHeight < targetHeight))
        {
            return;
        }

        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Crop,
            Position = AnchorPositionMode.Center,
            Size = new Size(targetWidth, targetHeight),
            Sampler = KnownResamplers.Lanczos3
        }));
    }

    private static void ResizeToDimensions(Image<Rgba32> image, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        if (sourceWidth == targetWidth && sourceHeight == targetHeight)
        {
            return;
        }

        image.Mutate(x => x.Resize(targetWidth, targetHeight, KnownResamplers.Lanczos3));
    }

    private static void ValidatePositiveDimension(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Dimension must be greater than zero.");
        }
    }
}
