using PixelPress.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Operations;

public sealed class OutputNamingOperation : IImageOperation
{
    private readonly Func<DateTime> _utcNowProvider;

    public OutputNamingOperation(string template, Func<DateTime>? utcNowProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        Template = template;
        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
    }

    public string Name => "output-naming";

    public string Template { get; }

    public Task ApplyAsync(Image<Rgba32> image, ImageJobContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(context.OutputPath) ?? Directory.GetCurrentDirectory();
        var inputName = Path.GetFileNameWithoutExtension(context.InputPath);
        var extensionWithDot = Path.GetExtension(context.OutputPath);

        if (string.IsNullOrWhiteSpace(extensionWithDot))
        {
            extensionWithDot = Path.GetExtension(context.InputPath);
        }

        var ext = extensionWithDot.TrimStart('.');
        var padding = Math.Max(3, context.Total.ToString().Length);
        var indexValue = (context.Index + 1).ToString($"D{padding}");
        var date = _utcNowProvider().ToString("yyyyMMdd");

        var expanded = Template
            .Replace("{name}", inputName, StringComparison.OrdinalIgnoreCase)
            .Replace("{index}", indexValue, StringComparison.OrdinalIgnoreCase)
            .Replace("{width}", image.Width.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{height}", image.Height.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", date, StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", ext, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(Path.GetExtension(expanded)) && !string.IsNullOrWhiteSpace(ext))
        {
            expanded = $"{expanded}.{ext}";
        }

        var safeFileName = SanitizeFileName(Path.GetFileName(expanded));
        context.OutputPath = Path.Combine(directory, safeFileName);

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException("Output naming template produced an empty file name.");
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = fileName;

        foreach (var invalid in invalidChars)
        {
            sanitized = sanitized.Replace(invalid, '_');
        }

        return sanitized;
    }
}
