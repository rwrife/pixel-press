using PixelPress.Core.Abstractions;
using PixelPress.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Tests;

public sealed class PipelineRunnerTests : IDisposable
{
    private readonly string _rootDirectory;

    public PipelineRunnerTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"pixelpress-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public async Task RunAsync_EmptyPipeline_ReencodesAllInputs()
    {
        var inputDirectory = CreateDirectory("input");
        var outputDirectory = CreateDirectory("output");

        var inputA = CreateImage(inputDirectory, "alpha.png", 24, 24, Color.Red);
        var inputB = CreateImage(inputDirectory, "bravo.jpg", 40, 30, Color.Blue);

        var runner = new PipelineRunner(new ImageSharpImageProcessor());
        var progressEvents = new List<PipelineProgress>();

        var result = await runner.RunAsync(
            new PipelineRunRequest
            {
                InputPaths = [inputA, inputB],
                OutputDirectory = outputDirectory,
                MaxDegreeOfParallelism = 2
            },
            progressEvents.Add,
            CancellationToken.None);

        Assert.Equal(2, result.TotalFiles);
        Assert.All(result.Files, file => Assert.Equal(FileProcessStatus.Success, file.Status));
        Assert.Equal(2, progressEvents.Count);

        Assert.True(File.Exists(Path.Combine(outputDirectory, "alpha.png")));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "bravo.jpg")));

        Assert.All(result.Files, file => Assert.True(file.InputBytes > 0));
        Assert.All(result.Files, file => Assert.True(file.OutputBytes > 0));
    }

    [Fact]
    public async Task RunAsync_IsolatesPerFileErrors_AndContinuesBatch()
    {
        var inputDirectory = CreateDirectory("input-errors");
        var outputDirectory = CreateDirectory("output-errors");

        var goodImage = CreateImage(inputDirectory, "good.png", 32, 32, Color.Green);
        var badInput = Path.Combine(inputDirectory, "bad.png");
        await File.WriteAllTextAsync(badInput, "not-an-image");

        var runner = new PipelineRunner(new ImageSharpImageProcessor());

        var result = await runner.RunAsync(
            new PipelineRunRequest
            {
                InputPaths = [goodImage, badInput],
                OutputDirectory = outputDirectory,
                MaxDegreeOfParallelism = 2
            });

        Assert.Equal(2, result.TotalFiles);
        Assert.Contains(result.Files, file => file.InputPath == goodImage && file.Status == FileProcessStatus.Success);

        var error = Assert.Single(result.Files.Where(file => file.InputPath == badInput));
        Assert.Equal(FileProcessStatus.Error, error.Status);
        Assert.NotNull(error.ErrorMessage);

        Assert.True(File.Exists(Path.Combine(outputDirectory, "good.png")));
    }

    [Fact]
    public async Task RunAsync_HonorsCancellation_AndReportsProgress()
    {
        var inputDirectory = CreateDirectory("input-cancel");
        var outputDirectory = CreateDirectory("output-cancel");

        var inputs = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            inputs.Add(CreateImage(inputDirectory, $"img-{i}.png", 32, 32, Color.Orange));
        }

        var runner = new PipelineRunner(new ImageSharpImageProcessor());
        var progressEvents = new List<PipelineProgress>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var result = await runner.RunAsync(
            new PipelineRunRequest
            {
                InputPaths = inputs,
                OutputDirectory = outputDirectory,
                MaxDegreeOfParallelism = 1,
                Operations = [new DelayOperation(TimeSpan.FromMilliseconds(500))]
            },
            progressEvents.Add,
            cts.Token);

        Assert.NotEmpty(progressEvents);
        Assert.True(result.TotalFiles < inputs.Count || result.Canceled > 0);
        Assert.Contains(result.Files, file => file.Status is FileProcessStatus.Canceled or FileProcessStatus.Success);
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
            // Best-effort cleanup only.
        }
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_rootDirectory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateImage(string directory, string fileName, int width, int height, Color color)
    {
        var path = Path.Combine(directory, fileName);
        using var image = new Image<Rgba32>(width, height, color);
        image.Save(path);
        return path;
    }

    private sealed class DelayOperation : IImageOperation
    {
        private readonly TimeSpan _delay;

        public DelayOperation(TimeSpan delay)
        {
            _delay = delay;
        }

        public string Name => "delay";

        public Task ApplyAsync(Image<Rgba32> image, ImageJobContext context, CancellationToken cancellationToken = default)
        {
            return Task.Delay(_delay, cancellationToken);
        }
    }
}
