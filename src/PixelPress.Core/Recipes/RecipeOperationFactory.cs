using PixelPress.Core.Abstractions;
using PixelPress.Core.Ai;
using PixelPress.Core.Operations;

namespace PixelPress.Core.Recipes;

public static class RecipeOperationFactory
{
    public static IReadOnlyList<IImageOperation> BuildOperations(
        IEnumerable<RecipeOperation> operationSettings,
        IImageAiService? aiService = null,
        bool enableSmartCrop = false,
        bool enableAutoNaming = false)
    {
        ArgumentNullException.ThrowIfNull(operationSettings);

        var operations = new List<IImageOperation>();

        foreach (var setting in operationSettings)
        {
            if (!setting.IsEnabled)
            {
                continue;
            }

            var operation = BuildOperation(setting, aiService, enableSmartCrop, enableAutoNaming);
            if (operation is not null)
            {
                operations.Add(operation);
            }
        }

        return operations;
    }

    private static IImageOperation? BuildOperation(
        RecipeOperation setting,
        IImageAiService? aiService,
        bool enableSmartCrop,
        bool enableAutoNaming)
    {
        return setting.Kind switch
        {
            RecipeOperationKind.Resize => BuildResizeOperation(setting, aiService, enableSmartCrop),
            RecipeOperationKind.Convert => new ConvertOperation(setting.TargetFormat),
            RecipeOperationKind.Compress => CompressOperation.QualityOnly(Math.Clamp(setting.Quality, 0, 100), setting.StripMetadata),
            RecipeOperationKind.Watermark => BuildWatermarkOperation(setting),
            RecipeOperationKind.Rename => BuildRenameOperation(setting, aiService, enableAutoNaming),
            _ => null
        };
    }

    private static IImageOperation BuildResizeOperation(
        RecipeOperation setting,
        IImageAiService? aiService,
        bool enableSmartCrop)
    {
        var width = Math.Max(1, setting.Width);
        var height = Math.Max(1, setting.Height);

        if (enableSmartCrop && setting.ResizeMode == ResizeMode.Fill)
        {
            return new AiSmartCropOperation(width, height, setting.AllowUpscale, aiService);
        }

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

    private static IImageOperation? BuildRenameOperation(
        RecipeOperation setting,
        IImageAiService? aiService,
        bool enableAutoNaming)
    {
        if (string.IsNullOrWhiteSpace(setting.RenameTemplate))
        {
            return null;
        }

        if (enableAutoNaming)
        {
            return new AiOutputNamingOperation(setting.RenameTemplate, aiService);
        }

        return new OutputNamingOperation(setting.RenameTemplate);
    }
}
