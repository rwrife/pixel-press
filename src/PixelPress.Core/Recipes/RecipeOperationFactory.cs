using PixelPress.Core.Abstractions;
using PixelPress.Core.Operations;

namespace PixelPress.Core.Recipes;

public static class RecipeOperationFactory
{
    public static IReadOnlyList<IImageOperation> BuildOperations(IEnumerable<RecipeOperation> operationSettings)
    {
        ArgumentNullException.ThrowIfNull(operationSettings);

        var operations = new List<IImageOperation>();

        foreach (var setting in operationSettings)
        {
            if (!setting.IsEnabled)
            {
                continue;
            }

            var operation = BuildOperation(setting);
            if (operation is not null)
            {
                operations.Add(operation);
            }
        }

        return operations;
    }

    private static IImageOperation? BuildOperation(RecipeOperation setting)
    {
        return setting.Kind switch
        {
            RecipeOperationKind.Resize => BuildResizeOperation(setting),
            RecipeOperationKind.Convert => new ConvertOperation(setting.TargetFormat),
            RecipeOperationKind.Compress => CompressOperation.QualityOnly(Math.Clamp(setting.Quality, 0, 100), setting.StripMetadata),
            RecipeOperationKind.Watermark => BuildWatermarkOperation(setting),
            RecipeOperationKind.Rename => BuildRenameOperation(setting),
            _ => null
        };
    }

    private static IImageOperation BuildResizeOperation(RecipeOperation setting)
    {
        var width = Math.Max(1, setting.Width);
        var height = Math.Max(1, setting.Height);

        return setting.ResizeMode switch
        {
            ResizeMode.LongestEdge => ResizeOperation.LongestEdge(width, setting.AllowUpscale),
            ResizeMode.ExactWxH => ResizeOperation.Exact(width, height, setting.AllowUpscale),
            ResizeMode.Percentage => ResizeOperation.Percentage(Math.Max(1, setting.Percentage), setting.AllowUpscale),
            ResizeMode.Fit => ResizeOperation.Fit(width, height, setting.AllowUpscale),
            ResizeMode.Fill => ResizeOperation.Fill(width, height, setting.AllowUpscale),
            _ => ResizeOperation.LongestEdge(width, setting.AllowUpscale)
        };
    }

    private static IImageOperation? BuildWatermarkOperation(RecipeOperation setting)
    {
        if (string.IsNullOrWhiteSpace(setting.WatermarkText))
        {
            return null;
        }

        return WatermarkOperation.TextOverlay(
            setting.WatermarkText,
            setting.WatermarkPosition,
            opacity: (float)Math.Clamp(setting.WatermarkOpacity, 0.0, 1.0),
            relativeScale: (float)Math.Clamp(setting.WatermarkScale, 0.02, 1.0),
            marginPixels: 16);
    }

    private static IImageOperation? BuildRenameOperation(RecipeOperation setting)
    {
        if (string.IsNullOrWhiteSpace(setting.RenameTemplate))
        {
            return null;
        }

        return new OutputNamingOperation(setting.RenameTemplate);
    }
}
