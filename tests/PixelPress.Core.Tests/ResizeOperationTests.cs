using PixelPress.Core.Operations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Tests;

public sealed class ResizeOperationTests
{
    [Fact]
    public async Task LongestEdge_ResizesPreservingAspectRatio()
    {
        using var image = new Image<Rgba32>(4000, 3000, Color.CornflowerBlue);

        await ResizeOperation.LongestEdge(1600).ApplyAsync(image, CreateContext());

        Assert.Equal(1600, image.Width);
        Assert.Equal(1200, image.Height);
    }

    [Fact]
    public async Task Percentage_ResizesByRequestedScale()
    {
        using var image = new Image<Rgba32>(1200, 800, Color.Green);

        await ResizeOperation.Percentage(50).ApplyAsync(image, CreateContext());

        Assert.Equal(600, image.Width);
        Assert.Equal(400, image.Height);
    }

    [Fact]
    public async Task Fill_ProducesExactDimensions()
    {
        using var image = new Image<Rgba32>(4000, 3000, Color.Red);

        await ResizeOperation.Fill(1000, 1000).ApplyAsync(image, CreateContext());

        Assert.Equal(1000, image.Width);
        Assert.Equal(1000, image.Height);
    }

    [Fact]
    public async Task Fit_ContainsImageWithinBounds()
    {
        using var image = new Image<Rgba32>(4000, 3000, Color.Orange);

        await ResizeOperation.Fit(1600, 1600).ApplyAsync(image, CreateContext());

        Assert.Equal(1600, image.Width);
        Assert.Equal(1200, image.Height);
    }

    [Fact]
    public async Task Exact_UsesExactDimensions()
    {
        using var image = new Image<Rgba32>(4000, 3000, Color.Purple);

        await ResizeOperation.Exact(1200, 900).ApplyAsync(image, CreateContext());

        Assert.Equal(1200, image.Width);
        Assert.Equal(900, image.Height);
    }

    [Fact]
    public async Task AllowUpscaleFalse_DoesNotUpscaleSmallerImage()
    {
        using var image = new Image<Rgba32>(800, 600, Color.Blue);

        await ResizeOperation.LongestEdge(1600, allowUpscale: false).ApplyAsync(image, CreateContext());

        Assert.Equal(800, image.Width);
        Assert.Equal(600, image.Height);
    }

    [Fact]
    public async Task AutoOrient_IsAppliedBeforeResize()
    {
        using var image = new Image<Rgba32>(1200, 800, Color.Beige);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);

        await ResizeOperation.LongestEdge(600).ApplyAsync(image, CreateContext());

        Assert.Equal(400, image.Width);
        Assert.Equal(600, image.Height);
    }

    private static ImageJobContext CreateContext() =>
        new("input.png", "output.png", 0, 1);
}
