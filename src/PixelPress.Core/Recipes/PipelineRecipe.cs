using PixelPress.Core.Operations;

namespace PixelPress.Core.Recipes;

public enum RecipeOperationKind
{
    Resize,
    Convert,
    Compress,
    Watermark,
    Rename
}

public sealed class RecipeOperation
{
    public RecipeOperationKind Kind { get; set; } = RecipeOperationKind.Resize;

    public bool IsEnabled { get; set; } = true;

    public ResizeMode ResizeMode { get; set; } = ResizeMode.LongestEdge;

    public int Width { get; set; } = 1600;

    public int Height { get; set; } = 1200;

    public double Percentage { get; set; } = 100;

    public bool AllowUpscale { get; set; }

    public OutputImageFormat TargetFormat { get; set; } = OutputImageFormat.Webp;

    public int Quality { get; set; } = 80;

    public bool StripMetadata { get; set; } = true;

    public string WatermarkText { get; set; } = "pixel-press";

    public WatermarkPosition WatermarkPosition { get; set; } = WatermarkPosition.BottomRight;

    public double WatermarkOpacity { get; set; } = 0.35;

    public double WatermarkScale { get; set; } = 0.20;

    public string RenameTemplate { get; set; } = "{name}-{index}.{ext}";
}

public sealed class PipelineRecipe
{
    public string Name { get; set; } = "Untitled Recipe";

    public string? Description { get; set; }

    public List<RecipeOperation> Operations { get; set; } = [];
}
