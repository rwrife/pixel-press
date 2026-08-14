using PixelPress.Core.Ai;
using PixelPress.Core.Operations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Tests;

public sealed class AiOperationsTests
{
    [Fact]
    public async Task SmartCrop_WithoutAiService_FallsBackToCenterCrop()
    {
        using var image = CreateThreeBandImage(width: 400, height: 200);
        var operation = new AiSmartCropOperation(width: 100, height: 100, allowUpscale: true, aiService: null);

        await operation.ApplyAsync(image, new ImageJobContext("in.png", "out.png", 0, 1));

        Assert.Equal(100, image.Width);
        Assert.Equal(100, image.Height);

        var center = image[50, 50];
        Assert.True(center.G > center.R && center.G > center.B, "Expected center-crop fallback to keep the middle (green) region dominant.");
    }

    [Fact]
    public async Task SmartCrop_WithAiFocusPoint_CropsTowardSuggestedRegion()
    {
        using var image = CreateThreeBandImage(width: 400, height: 200);
        var fakeAi = new FakeAiService(cropFocus: new CropFocusPoint(0.95, 0.5), fileName: null, reachable: true);
        var operation = new AiSmartCropOperation(width: 100, height: 100, allowUpscale: true, aiService: fakeAi);

        await operation.ApplyAsync(image, new ImageJobContext("in.png", "out.png", 0, 1));

        Assert.Equal(100, image.Width);
        Assert.Equal(100, image.Height);

        var center = image[50, 50];
        Assert.True(center.B > center.R && center.B > center.G, "Expected AI focus near the right side to favor the blue region.");
    }

    [Fact]
    public async Task AiOutputNaming_UsesAiSuggestionWhenAvailable()
    {
        using var image = new Image<Rgba32>(16, 16, Color.CornflowerBlue);
        var fakeAi = new FakeAiService(cropFocus: null, fileName: "Sunset Over Lake!", reachable: true);
        var operation = new AiOutputNamingOperation("{name}-{index}.{ext}", fakeAi);
        var context = new ImageJobContext("input-photo.png", Path.Combine(Path.GetTempPath(), "result.webp"), 0, 1);

        await operation.ApplyAsync(image, context);

        Assert.Equal("sunset-over-lake.webp", Path.GetFileName(context.OutputPath));
    }

    [Fact]
    public async Task AiOutputNaming_FallsBackToTemplateWhenAiUnavailable()
    {
        using var image = new Image<Rgba32>(16, 16, Color.CornflowerBlue);
        var fakeAi = new FakeAiService(cropFocus: null, fileName: null, reachable: false);
        var operation = new AiOutputNamingOperation("{name}-{index}.{ext}", fakeAi);
        var context = new ImageJobContext("input-photo.png", Path.Combine(Path.GetTempPath(), "result.webp"), 0, 1);

        await operation.ApplyAsync(image, context);

        Assert.Equal("input-photo-001.webp", Path.GetFileName(context.OutputPath));
    }

    private static Image<Rgba32> CreateThreeBandImage(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image[x, y] = x switch
                {
                    < 133 => new Rgba32(255, 0, 0, 255),
                    < 267 => new Rgba32(0, 255, 0, 255),
                    _ => new Rgba32(0, 0, 255, 255)
                };
            }
        }

        return image;
    }

    private sealed class FakeAiService : IImageAiService
    {
        private readonly CropFocusPoint? _cropFocus;
        private readonly string? _fileName;
        private readonly bool _reachable;

        public FakeAiService(CropFocusPoint? cropFocus, string? fileName, bool reachable)
        {
            _cropFocus = cropFocus;
            _fileName = fileName;
            _reachable = reachable;
        }

        public Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_reachable);

        public Task<CropFocusPoint?> SuggestCropFocusAsync(
            Image<Rgba32> image,
            string inputPath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_reachable ? _cropFocus : null);
        }

        public Task<string?> SuggestFileNameAsync(
            Image<Rgba32> image,
            string inputPath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_reachable ? _fileName : null);
        }
    }
}
