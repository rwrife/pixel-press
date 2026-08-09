using PixelPress.Core.Abstractions;
using PixelPress.Core.Imaging;
using PixelPress.Core.Operations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Tests;

public sealed class ConvertAndCompressOperationTests : IDisposable
{
    private readonly string _rootDirectory;

    public ConvertAndCompressOperationTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"pixelpress-convert-compress-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public async Task ConvertOperation_ProducesOpenableWebpAndJpegOutputs()
    {
        var inputDirectory = CreateDirectory("convert-input");
        var outputWebpDirectory = CreateDirectory("convert-webp");
        var outputJpegDirectory = CreateDirectory("convert-jpeg");
        var inputPath = CreateNoisyImage(inputDirectory, "sample.png", 256, 192, seed: 42);

        var webpResult = await RunSingleAsync(inputPath, outputWebpDirectory, ConvertOperation.ToWebp());
        var jpegResult = await RunSingleAsync(inputPath, outputJpegDirectory, ConvertOperation.ToJpeg());

        Assert.Equal(FileProcessStatus.Success, webpResult.Status);
        Assert.Equal(FileProcessStatus.Success, jpegResult.Status);

        Assert.NotNull(webpResult.OutputPath);
        Assert.NotNull(jpegResult.OutputPath);

        Assert.EndsWith(".webp", webpResult.OutputPath!, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".jpg", jpegResult.OutputPath!, StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(webpResult.OutputPath));
        Assert.True(File.Exists(jpegResult.OutputPath));

        using var openableWebp = await Image.LoadAsync<Rgba32>(webpResult.OutputPath!);
        using var openableJpeg = await Image.LoadAsync<Rgba32>(jpegResult.OutputPath!);
        Assert.True(openableWebp.Width > 0 && openableWebp.Height > 0);
        Assert.True(openableJpeg.Width > 0 && openableJpeg.Height > 0);

        Assert.True(IsWebpSignature(webpResult.OutputPath!));
        Assert.True(IsJpegSignature(jpegResult.OutputPath!));
    }

    [Fact]
    public async Task CompressOperation_QualityMode_LowerQualityProducesSmallerJpeg()
    {
        var inputDirectory = CreateDirectory("quality-input");
        var highOutputDirectory = CreateDirectory("quality-high");
        var lowOutputDirectory = CreateDirectory("quality-low");
        var inputPath = CreateNoisyImage(inputDirectory, "quality-source.png", 1200, 800, seed: 7);

        var highResult = await RunSingleAsync(
            inputPath,
            highOutputDirectory,
            ConvertOperation.ToJpeg(),
            CompressOperation.QualityOnly(95));

        var lowResult = await RunSingleAsync(
            inputPath,
            lowOutputDirectory,
            ConvertOperation.ToJpeg(),
            CompressOperation.QualityOnly(35));

        Assert.Equal(FileProcessStatus.Success, highResult.Status);
        Assert.Equal(FileProcessStatus.Success, lowResult.Status);
        Assert.True(lowResult.OutputBytes < highResult.OutputBytes, $"Expected low quality output ({lowResult.OutputBytes}) to be smaller than high quality output ({highResult.OutputBytes}).");
    }

    [Fact]
    public async Task CompressOperation_TargetSizeMode_KeepsOutputWithinBudget_WhenPossible()
    {
        var inputDirectory = CreateDirectory("budget-input");
        var outputDirectory = CreateDirectory("budget-output");
        var inputPath = CreateNoisyImage(inputDirectory, "budget-source.png", 900, 700, seed: 121);
        const long budgetBytes = 45 * 1024;

        var result = await RunSingleAsync(
            inputPath,
            outputDirectory,
            ConvertOperation.ToJpeg(),
            CompressOperation.TargetFileSize(budgetBytes));

        Assert.Equal(FileProcessStatus.Success, result.Status);
        Assert.True(result.OutputBytes <= budgetBytes, $"Expected output <= {budgetBytes}, got {result.OutputBytes}.");
    }

    [Fact]
    public async Task CompressOperation_TargetSizeMode_ReportsWhenBudgetCannotBeMet()
    {
        var inputDirectory = CreateDirectory("impossible-input");
        var outputDirectory = CreateDirectory("impossible-output");
        var inputPath = CreateNoisyImage(inputDirectory, "impossible-source.png", 1200, 900, seed: 11);
        const long impossibleBudgetBytes = 400;

        var result = await RunSingleAsync(
            inputPath,
            outputDirectory,
            ConvertOperation.ToJpeg(),
            CompressOperation.TargetFileSize(impossibleBudgetBytes, minQuality: 0, maxQuality: 25));

        Assert.Equal(FileProcessStatus.Error, result.Status);
        Assert.Contains("could not be met", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task CompressOperation_StripMetadata_RemovesExifAndPreservesOrientation()
    {
        var inputDirectory = CreateDirectory("metadata-input");
        var outputDirectory = CreateDirectory("metadata-output");

        var inputPath = Path.Combine(inputDirectory, "oriented.jpg");
        using (var image = new Image<Rgba32>(80, 40, Color.CadetBlue))
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
            await image.SaveAsJpegAsync(inputPath, new JpegEncoder { Quality = 95 });
        }

        var result = await RunSingleAsync(
            inputPath,
            outputDirectory,
            ConvertOperation.ToJpeg(),
            CompressOperation.QualityOnly(90, stripMetadata: true));

        Assert.Equal(FileProcessStatus.Success, result.Status);

        using var outputImage = await Image.LoadAsync<Rgba32>(result.OutputPath!);
        Assert.Null(outputImage.Metadata.ExifProfile);
        Assert.Equal(40, outputImage.Width);
        Assert.Equal(80, outputImage.Height);
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

    private async Task<FileProcessResult> RunSingleAsync(string inputPath, string outputDirectory, params IImageOperation[] operations)
    {
        var runner = new PipelineRunner(new ImageSharpImageProcessor());

        var batch = await runner.RunAsync(new PipelineRunRequest
        {
            InputPaths = [inputPath],
            OutputDirectory = outputDirectory,
            Operations = operations,
            MaxDegreeOfParallelism = 1
        });

        return Assert.Single(batch.Files);
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_rootDirectory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateNoisyImage(string directory, string fileName, int width, int height, int seed)
    {
        var path = Path.Combine(directory, fileName);
        var random = new Random(seed);

        using var image = new Image<Rgba32>(width, height);

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

        image.SaveAsPng(path);
        return path;
    }

    private static bool IsJpegSignature(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
    }

    private static bool IsWebpSignature(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return bytes.Length >= 12
               && bytes[0] == (byte)'R'
               && bytes[1] == (byte)'I'
               && bytes[2] == (byte)'F'
               && bytes[3] == (byte)'F'
               && bytes[8] == (byte)'W'
               && bytes[9] == (byte)'E'
               && bytes[10] == (byte)'B'
               && bytes[11] == (byte)'P';
    }
}
