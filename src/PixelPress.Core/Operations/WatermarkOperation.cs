using PixelPress.Core.Abstractions;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PixelPress.Core.Operations;

public enum WatermarkPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    Center,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public sealed class WatermarkOperation : IImageOperation
{
    private const float DefaultOpacity = 0.35f;
    private const float DefaultRelativeScale = 0.2f;

    private WatermarkOperation(
        string? text,
        string? watermarkImagePath,
        WatermarkPosition position,
        float opacity,
        float relativeScale,
        int marginPixels,
        Color color)
    {
        if (opacity is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        if (relativeScale <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeScale), "Relative scale must be greater than zero.");
        }

        if (marginPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(marginPixels), "Margin cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(watermarkImagePath))
        {
            throw new ArgumentException("Either watermark text or watermark image path must be provided.");
        }

        Text = text;
        WatermarkImagePath = watermarkImagePath;
        Position = position;
        Opacity = opacity;
        RelativeScale = relativeScale;
        MarginPixels = marginPixels;
        Color = color;
    }

    public string Name => "watermark";

    public string? Text { get; }

    public string? WatermarkImagePath { get; }

    public WatermarkPosition Position { get; }

    public float Opacity { get; }

    public float RelativeScale { get; }

    public int MarginPixels { get; }

    public Color Color { get; }

    public static WatermarkOperation TextOverlay(
        string text,
        WatermarkPosition position = WatermarkPosition.BottomRight,
        float opacity = DefaultOpacity,
        float relativeScale = DefaultRelativeScale,
        int marginPixels = 16,
        Color? color = null)
        => new(text, null, position, opacity, relativeScale, marginPixels, color ?? Color.White);

    public static WatermarkOperation ImageOverlay(
        string watermarkImagePath,
        WatermarkPosition position = WatermarkPosition.BottomRight,
        float opacity = DefaultOpacity,
        float relativeScale = DefaultRelativeScale,
        int marginPixels = 16)
        => new(null, watermarkImagePath, position, opacity, relativeScale, marginPixels, Color.White);

    public async Task ApplyAsync(Image<Rgba32> image, ImageJobContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        using var overlay = await BuildOverlayAsync(image.Width, image.Height, cancellationToken);

        var location = CalculatePlacement(
            image.Width,
            image.Height,
            overlay.Width,
            overlay.Height,
            Position,
            MarginPixels);

        image.Mutate(x => x.DrawImage(overlay, location, Opacity));
    }

    public static Point CalculatePlacement(
        int canvasWidth,
        int canvasHeight,
        int watermarkWidth,
        int watermarkHeight,
        WatermarkPosition position,
        int marginPixels)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(canvasWidth), "Canvas dimensions must be greater than zero.");
        }

        if (watermarkWidth <= 0 || watermarkHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(watermarkWidth), "Watermark dimensions must be greater than zero.");
        }

        marginPixels = Math.Max(0, marginPixels);

        var left = marginPixels;
        var centerX = (canvasWidth - watermarkWidth) / 2;
        var right = canvasWidth - watermarkWidth - marginPixels;

        var top = marginPixels;
        var centerY = (canvasHeight - watermarkHeight) / 2;
        var bottom = canvasHeight - watermarkHeight - marginPixels;

        var x = position switch
        {
            WatermarkPosition.TopLeft or WatermarkPosition.MiddleLeft or WatermarkPosition.BottomLeft => left,
            WatermarkPosition.TopCenter or WatermarkPosition.Center or WatermarkPosition.BottomCenter => centerX,
            _ => right
        };

        var y = position switch
        {
            WatermarkPosition.TopLeft or WatermarkPosition.TopCenter or WatermarkPosition.TopRight => top,
            WatermarkPosition.MiddleLeft or WatermarkPosition.Center or WatermarkPosition.MiddleRight => centerY,
            _ => bottom
        };

        x = Math.Clamp(x, 0, Math.Max(0, canvasWidth - watermarkWidth));
        y = Math.Clamp(y, 0, Math.Max(0, canvasHeight - watermarkHeight));

        return new Point(x, y);
    }

    private async Task<Image<Rgba32>> BuildOverlayAsync(int baseWidth, int baseHeight, CancellationToken cancellationToken)
    {
        var maxOverlayWidth = Math.Max(1, baseWidth - (MarginPixels * 2));
        var requestedWidth = Math.Max(1, (int)Math.Round(baseWidth * RelativeScale));
        var targetWidth = Math.Clamp(requestedWidth, 1, maxOverlayWidth);

        if (!string.IsNullOrWhiteSpace(WatermarkImagePath))
        {
            var watermark = await Image.LoadAsync<Rgba32>(WatermarkImagePath, cancellationToken);
            ResizeOverlayInPlace(watermark, targetWidth, baseHeight);
            return watermark;
        }

        return BuildTextOverlay(targetWidth, baseHeight);
    }

    private Image<Rgba32> BuildTextOverlay(int targetWidth, int baseHeight)
    {
        var text = Text ?? string.Empty;
        if (text.Length == 0)
        {
            return new Image<Rgba32>(1, 1);
        }

        var families = SystemFonts.Collection.Families;
        if (!families.Any())
        {
            return BuildFallbackTextOverlay(targetWidth, baseHeight, text);
        }

        var family = families.First();
        const float baseFontSize = 24f;
        var baseFont = family.CreateFont(baseFontSize, FontStyle.Regular);
        var baseMeasure = TextMeasurer.MeasureSize(text, new TextOptions(baseFont));

        if (baseMeasure.Width <= 0 || baseMeasure.Height <= 0)
        {
            return BuildFallbackTextOverlay(targetWidth, baseHeight, text);
        }

        var scale = targetWidth / baseMeasure.Width;
        var finalSize = Math.Max(8f, baseFontSize * scale);
        var font = family.CreateFont(finalSize, FontStyle.Regular);
        var finalMeasure = TextMeasurer.MeasureSize(text, new TextOptions(font));

        var width = Math.Max(1, (int)Math.Ceiling(finalMeasure.Width));
        var height = Math.Max(1, (int)Math.Ceiling(finalMeasure.Height));
        var maxHeight = Math.Max(1, (int)Math.Round(baseHeight * RelativeScale));

        var overlay = new Image<Rgba32>(width, height);
        overlay.Mutate(x => x.DrawText(text, font, Color, new PointF(0, 0)));

        if (overlay.Height > maxHeight)
        {
            var constrainedWidth = Math.Max(1, (int)Math.Round(overlay.Width * (maxHeight / (double)overlay.Height)));
            overlay.Mutate(x => x.Resize(constrainedWidth, maxHeight));
        }

        return overlay;
    }

    private Image<Rgba32> BuildFallbackTextOverlay(int targetWidth, int baseHeight, string text)
    {
        var normalizedLength = Math.Max(1, text.Trim().Length);
        var height = Math.Max(8, Math.Min((int)Math.Round(baseHeight * RelativeScale * 0.5), 96));
        var width = Math.Max(8, targetWidth);
        var overlay = new Image<Rgba32>(width, height);

        var stripeWidth = Math.Max(1, width / (normalizedLength * 2));

        overlay.Mutate(ctx =>
        {
            ctx.Clear(Color.Transparent);

            for (var i = 0; i < normalizedLength; i++)
            {
                var x = i * stripeWidth * 2;
                if (x >= width)
                {
                    break;
                }

                var rectWidth = Math.Min(stripeWidth, width - x);
                ctx.Fill(Color, new Rectangle(x, 0, rectWidth, height));
            }
        });

        return overlay;
    }

    private static void ResizeOverlayInPlace(Image<Rgba32> overlay, int targetWidth, int baseHeight)
    {
        if (overlay.Width <= 0 || overlay.Height <= 0)
        {
            return;
        }

        if (overlay.Width == targetWidth)
        {
            return;
        }

        var targetHeight = Math.Max(1, (int)Math.Round(overlay.Height * (targetWidth / (double)overlay.Width)));
        var maxHeight = Math.Max(1, baseHeight);
        targetHeight = Math.Min(targetHeight, maxHeight);

        overlay.Mutate(x => x.Resize(targetWidth, targetHeight));
    }
}
