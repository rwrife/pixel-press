using PixelPress.Core.Imaging;
using PixelPress.Core.Recipes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Tests;

public sealed class RecipeSerializationTests : IDisposable
{
    private readonly string _rootDirectory;

    public RecipeSerializationTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"pixelpress-recipe-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public void RecipeJson_RoundTripsPipelineSettings()
    {
        var original = new PipelineRecipe
        {
            Name = "roundtrip-test",
            Description = "Ensures recipe settings survive JSON serialization.",
            Operations =
            [
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Resize,
                    ResizeMode = PixelPress.Core.Operations.ResizeMode.Fit,
                    Width = 1600,
                    Height = 900,
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
                    Quality = 74,
                    StripMetadata = true
                },
                new RecipeOperation
                {
                    Kind = RecipeOperationKind.Rename,
                    RenameTemplate = "{name}-web-{index}.{ext}"
                }
            ]
        };

        var json = RecipeJson.Serialize(original);
        var rehydrated = RecipeJson.Deserialize(json);

        Assert.Equal(original.Name, rehydrated.Name);
        Assert.Equal(original.Description, rehydrated.Description);
        Assert.Equal(original.Operations.Count, rehydrated.Operations.Count);

        for (var i = 0; i < original.Operations.Count; i++)
        {
            var expected = original.Operations[i];
            var actual = rehydrated.Operations[i];

            Assert.Equal(expected.Kind, actual.Kind);
            Assert.Equal(expected.IsEnabled, actual.IsEnabled);
            Assert.Equal(expected.ResizeMode, actual.ResizeMode);
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            Assert.Equal(expected.Percentage, actual.Percentage);
            Assert.Equal(expected.AllowUpscale, actual.AllowUpscale);
            Assert.Equal(expected.TargetFormat, actual.TargetFormat);
            Assert.Equal(expected.Quality, actual.Quality);
            Assert.Equal(expected.StripMetadata, actual.StripMetadata);
            Assert.Equal(expected.WatermarkText, actual.WatermarkText);
            Assert.Equal(expected.WatermarkPosition, actual.WatermarkPosition);
            Assert.Equal(expected.WatermarkOpacity, actual.WatermarkOpacity);
            Assert.Equal(expected.WatermarkScale, actual.WatermarkScale);
            Assert.Equal(expected.RenameTemplate, actual.RenameTemplate);
        }
    }

    [Fact]
    public async Task BuiltInPresets_AreAvailableAndRunnable()
    {
        Assert.True(RecipeCatalog.BuiltInPresets.Count >= 2);
        Assert.Contains(RecipeCatalog.BuiltInPresets, preset => string.Equals(preset.Name, "Web export", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(RecipeCatalog.BuiltInPresets, preset => string.Equals(preset.Name, "Email compress", StringComparison.OrdinalIgnoreCase));

        var inputDirectory = Path.Combine(_rootDirectory, "input");
        var outputDirectory = Path.Combine(_rootDirectory, "output");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var inputPath = Path.Combine(inputDirectory, "sample.png");
        using (var image = new Image<Rgba32>(1024, 768, Color.CornflowerBlue))
        {
            await image.SaveAsPngAsync(inputPath);
        }

        var recipe = RecipeCatalog.FindBuiltIn("Web export");
        Assert.NotNull(recipe);

        var operations = RecipeOperationFactory.BuildOperations(recipe!.Operations);
        var runner = new PipelineRunner(new ImageSharpImageProcessor());

        var batch = await runner.RunAsync(new PipelineRunRequest
        {
            InputPaths = [inputPath],
            OutputDirectory = outputDirectory,
            Operations = operations,
            MaxDegreeOfParallelism = 1
        });

        var file = Assert.Single(batch.Files);
        Assert.Equal(FileProcessStatus.Success, file.Status);
        Assert.NotNull(file.OutputPath);
        Assert.True(File.Exists(file.OutputPath!));
        Assert.EndsWith(".webp", file.OutputPath!, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }
}
