using PixelPress.Core.Imaging;
using PixelPress.Core.Operations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Tests;

public sealed class OutputNamingAndWatermarkOperationTests : IDisposable
{
    private readonly string _rootDirectory;

    public OutputNamingAndWatermarkOperationTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"pixelpress-output-watermark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public async Task OutputNaming_TemplateWithIndex_ProducesSequentialUniqueNames()
    {
        var inputA = CreateSolidImage(CreateDirectory("a"), "photo.png", 80, 60, Color.CadetBlue);
        var inputB = CreateSolidImage(CreateDirectory("b"), "photo.jpg", 90, 50, Color.Coral);
        var inputC = CreateSolidImage(CreateDirectory("c"), "landscape.png", 120, 80, Color.Goldenrod);
        var outputDirectory = CreateDirectory("output-sequential");

        var runner = new PipelineRunner(new ImageSharpImageProcessor());
        var batch = await runner.RunAsync(new PipelineRunRequest
        {
            InputPaths = [inputA, inputB, inputC],
            OutputDirectory = outputDirectory,
            MaxDegreeOfParallelism = 1,
            Operations =
            [
                ConvertOperation.ToWebp(),
                new OutputNamingOperation("{name}-{index}.{ext}", () => new DateTime(2026, 08, 10, 0, 0, 0, DateTimeKind.Utc))
            ]
        });

        Assert.Equal(3, batch.TotalFiles);
        Assert.All(batch.Files, file => Assert.Equal(FileProcessStatus.Success, file.Status));

        var fileNames = batch.Files
            .Select(file => Path.GetFileName(file.OutputPath))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(3, fileNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("photo-001.webp", fileNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("photo-002.webp", fileNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("landscape-003.webp", fileNames, StringComparer.OrdinalIgnoreCase);

        Assert.All(batch.Files, file => Assert.True(File.Exists(file.OutputPath!)));
    }

    [Fact]
    public async Task OutputNaming_CollidingTemplate_UsesSuffixToAvoidOverwrite()
    {
        var inputA = CreateSolidImage(CreateDirectory("x"), "duplicate.png", 64, 64, Color.Aquamarine);
        var inputB = CreateSolidImage(CreateDirectory("y"), "duplicate.png", 64, 64, Color.Brown);
        var outputDirectory = CreateDirectory("output-collision");

        var runner = new PipelineRunner(new ImageSharpImageProcessor());
        var batch = await runner.RunAsync(new PipelineRunRequest
        {
            InputPaths = [inputA, inputB],
            OutputDirectory = outputDirectory,
            MaxDegreeOfParallelism = 2,
            Operations = [new OutputNamingOperation("{name}.{ext}")]
        });

        Assert.All(batch.Files, file => Assert.Equal(FileProcessStatus.Success, file.Status));

        var names = batch.Files
            .Select(file => Path.GetFileName(file.OutputPath))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(2, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("duplicate.png", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("duplicate-1.png", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void WatermarkPlacementMath_ReturnsExpectedGridCoordinates()
    {
        Assert.Equal(new Point(10, 10), WatermarkOperation.CalculatePlacement(200, 120, 40, 20, WatermarkPosition.TopLeft, 10));
        Assert.Equal(new Point(80, 10), WatermarkOperation.CalculatePlacement(200, 120, 40, 20, WatermarkPosition.TopCenter, 10));
        Assert.Equal(new Point(150, 90), WatermarkOperation.CalculatePlacement(200, 120, 40, 20, WatermarkPosition.BottomRight, 10));
        Assert.Equal(new Point(80, 50), WatermarkOperation.CalculatePlacement(200, 120, 40, 20, WatermarkPosition.Center, 10));
    }

    [Fact]
    public async Task WatermarkOperation_RendersTextAndImageAtRequestedPosition()
    {
        using var textImage = new Image<Rgba32>(200, 100, Color.White);
        var textOperation = WatermarkOperation.TextOverlay(
            "WM",
            position: WatermarkPosition.TopLeft,
            opacity: 1f,
            relativeScale: 0.3f,
            marginPixels: 0,
            color: Color.Black);

        await textOperation.ApplyAsync(textImage, new ImageJobContext("in.png", "out.png", 0, 1));

        var textBounds = FindChangedBounds(textImage, Color.White);
        Assert.NotNull(textBounds);
        Assert.True(textBounds!.Value.Left <= 5);
        Assert.True(textBounds.Value.Top <= 5);

        var logoPath = Path.Combine(CreateDirectory("logo"), "logo.png");
        using (var logo = new Image<Rgba32>(20, 20, Color.Red))
        {
            await logo.SaveAsPngAsync(logoPath);
        }

        using var image = new Image<Rgba32>(200, 100, Color.White);
        var imageOperation = WatermarkOperation.ImageOverlay(
            logoPath,
            position: WatermarkPosition.BottomRight,
            opacity: 0.5f,
            relativeScale: 0.25f,
            marginPixels: 0);

        await imageOperation.ApplyAsync(image, new ImageJobContext("in.png", "out.png", 0, 1));

        var bottomRight = image[199, 99];
        Assert.True(bottomRight.R > bottomRight.G, "Expected bottom-right pixel to be tinted red by the watermark.");
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[5, 5]);
    }

    [Fact]
    public async Task InPlaceWithBackup_RestoresOriginal_WhenSaveFails()
    {
        var inputDirectory = CreateDirectory("in-place-input");
        var outputDirectory = CreateDirectory("in-place-output");
        var inputPath = Path.Combine(inputDirectory, "source.jpg");

        using (var image = CreateNoisyImage(1200, 900, seed: 123))
        {
            await image.SaveAsJpegAsync(inputPath, new JpegEncoder { Quality = 95 });
        }

        var originalBytes = await File.ReadAllBytesAsync(inputPath);

        var runner = new PipelineRunner(new ImageSharpImageProcessor());
        var batch = await runner.RunAsync(new PipelineRunRequest
        {
            InputPaths = [inputPath],
            OutputDirectory = outputDirectory,
            MaxDegreeOfParallelism = 1,
            WriteInPlace = true,
            CreateBackupWhenInPlace = true,
            Operations = [CompressOperation.TargetFileSize(300, minQuality: 0, maxQuality: 5)]
        });

        var result = Assert.Single(batch.Files);
        Assert.Equal(FileProcessStatus.Error, result.Status);

        var backupPath = inputPath + ".bak";
        Assert.True(File.Exists(backupPath), "Expected .bak file to be created in in-place backup mode.");

        var currentBytes = await File.ReadAllBytesAsync(inputPath);
        Assert.True(originalBytes.SequenceEqual(currentBytes), "Expected original input bytes to be restored after failure.");
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

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_rootDirectory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateSolidImage(string directory, string fileName, int width, int height, Color color)
    {
        var path = Path.Combine(directory, fileName);
        using var image = new Image<Rgba32>(width, height, color);
        image.Save(path);
        return path;
    }

    private static Image<Rgba32> CreateNoisyImage(int width, int height, int seed)
    {
        var random = new Random(seed);
        var image = new Image<Rgba32>(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image[x, y] = new Rgba32(
                    (byte)random.Next(0, 256),
                    (byte)random.Next(0, 256),
                    (byte)random.Next(0, 256),
                    255);
            }
        }

        return image;
    }

    private static Rectangle? FindChangedBounds(Image<Rgba32> image, Color baselineColor)
    {
        var baseline = baselineColor.ToPixel<Rgba32>();
        var found = false;
        var left = image.Width;
        var top = image.Height;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].Equals(baseline))
                {
                    continue;
                }

                found = true;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (!found)
        {
            return null;
        }

        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
