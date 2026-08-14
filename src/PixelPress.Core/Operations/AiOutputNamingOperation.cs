using PixelPress.Core.Ai;
using PixelPress.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Operations;

public sealed class AiOutputNamingOperation : IImageOperation
{
    private readonly IImageAiService? _aiService;
    private readonly OutputNamingOperation _fallback;

    public AiOutputNamingOperation(string fallbackTemplate, IImageAiService? aiService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackTemplate);
        _fallback = new OutputNamingOperation(fallbackTemplate);
        _aiService = aiService;
    }

    public string Name => "ai-output-naming";

    public async Task ApplyAsync(Image<Rgba32> image, ImageJobContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (_aiService is not null)
        {
            try
            {
                var suggestion = await _aiService.SuggestFileNameAsync(image, context.InputPath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(suggestion))
                {
                    var extension = ResolveExtension(context);
                    var safeStem = SanitizeFileNameStem(suggestion);

                    if (!string.IsNullOrWhiteSpace(safeStem))
                    {
                        var directory = Path.GetDirectoryName(context.OutputPath) ?? Directory.GetCurrentDirectory();
                        var fileName = string.IsNullOrWhiteSpace(extension)
                            ? safeStem
                            : $"{safeStem}.{extension}";

                        context.OutputPath = Path.Combine(directory, fileName);
                        return;
                    }
                }
            }
            catch
            {
                // Graceful fallback below.
            }
        }

        await _fallback.ApplyAsync(image, context, cancellationToken);
    }

    private static string ResolveExtension(ImageJobContext context)
    {
        var extension = Path.GetExtension(context.OutputPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = Path.GetExtension(context.InputPath);
        }

        return extension.TrimStart('.');
    }

    private static string SanitizeFileNameStem(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var chars = normalized
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        if (slug.Length > 80)
        {
            slug = slug[..80].Trim('-');
        }

        return slug;
    }
}
