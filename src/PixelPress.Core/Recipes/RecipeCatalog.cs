using PixelPress.Core.Operations;

namespace PixelPress.Core.Recipes;

public static class RecipeCatalog
{
    public static IReadOnlyList<PipelineRecipe> BuiltInPresets { get; } =
    [
        BuildWebExportPreset(),
        BuildEmailCompressPreset(),
        BuildThumbnailPreset()
    ];

    public static PipelineRecipe? FindBuiltIn(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var slug = RecipeStore.Slugify(name);

        return BuiltInPresets.FirstOrDefault(preset =>
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(RecipeStore.Slugify(preset.Name), slug, StringComparison.OrdinalIgnoreCase));
    }

    private static PipelineRecipe BuildWebExportPreset()
    {
        return new PipelineRecipe
        {
            Name = "Web export",
            Description = "Resize to 1600px longest edge, convert to WebP, and compress for web delivery.",
            Operations =
            [
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Resize,
                    ResizeMode = ResizeMode.LongestEdge,
                    Width = 1600,
                    AllowUpscale = false
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Convert,
                    TargetFormat = OutputImageFormat.Webp
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Compress,
                    Quality = 80,
                    StripMetadata = true
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Rename,
                    RenameTemplate = "{name}-web-{index}.{ext}"
                }
            ]
        };
    }

    private static PipelineRecipe BuildEmailCompressPreset()
    {
        return new PipelineRecipe
        {
            Name = "Email compress",
            Description = "Resize for email attachments and export JPEG with medium quality.",
            Operations =
            [
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Resize,
                    ResizeMode = ResizeMode.LongestEdge,
                    Width = 1280,
                    AllowUpscale = false
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Convert,
                    TargetFormat = OutputImageFormat.Jpeg
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Compress,
                    Quality = 72,
                    StripMetadata = true
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Rename,
                    RenameTemplate = "{name}-email-{index}.{ext}"
                }
            ]
        };
    }

    private static PipelineRecipe BuildThumbnailPreset()
    {
        return new PipelineRecipe
        {
            Name = "Thumbnails",
            Description = "Create fixed-size center-cropped thumbnails.",
            Operations =
            [
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Resize,
                    ResizeMode = ResizeMode.Fill,
                    Width = 512,
                    Height = 512,
                    AllowUpscale = true
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Convert,
                    TargetFormat = OutputImageFormat.Webp
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Compress,
                    Quality = 82,
                    StripMetadata = true
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Rename,
                    RenameTemplate = "thumb-{name}-{index}.{ext}"
                }
            ]
        };
    }
}
