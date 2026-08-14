using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelPress.Core.Ai;

public sealed class LocalOpenAiImageAiService : IImageAiService
{
    private static readonly Regex NumberRegex = new("-?\\d+(?:\\.\\d+)?", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly Uri _chatCompletionsUri;
    private readonly Uri[] _probeUris;

    public LocalOpenAiImageAiService(Uri endpoint, string model, string? apiKey = null, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (!endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("AI endpoint must be an absolute URI.", nameof(endpoint));
        }

        if (!IsLoopbackEndpoint(endpoint))
        {
            throw new ArgumentException("AI endpoint must be local-only (localhost / 127.0.0.1 / ::1).", nameof(endpoint));
        }

        _model = model.Trim();
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(8);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        var endpointText = endpoint.ToString().TrimEnd('/');
        var endpointEndsWithV1 = endpointText.EndsWith("/v1", StringComparison.OrdinalIgnoreCase);

        if (endpointEndsWithV1)
        {
            _chatCompletionsUri = new Uri($"{endpointText}/chat/completions");
            var root = endpointText[..^3].TrimEnd('/');
            _probeUris =
            [
                new Uri($"{endpointText}/models"),
                new Uri($"{root}/api/tags")
            ];
        }
        else
        {
            _chatCompletionsUri = new Uri($"{endpointText}/v1/chat/completions");
            _probeUris =
            [
                new Uri($"{endpointText}/v1/models"),
                new Uri($"{endpointText}/api/tags")
            ];
        }
    }

    public static bool TryResolveLoopbackEndpoint(string endpoint, out Uri? uri, out string? failureReason)
    {
        uri = null;
        failureReason = null;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed))
        {
            failureReason = "AI endpoint is not a valid absolute URI.";
            return false;
        }

        if (!IsLoopbackEndpoint(parsed))
        {
            failureReason = "AI endpoint must stay local-only (localhost / 127.0.0.1 / ::1).";
            return false;
        }

        uri = parsed;
        return true;
    }

    public Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        return ProbeReachabilityAsync(cancellationToken);
    }

    public async Task<CropFocusPoint?> SuggestCropFocusAsync(
        Image<Rgba32> image,
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        var prompt = $$"""
            Analyze this image and choose the best visual focus point for a crop.
            Return ONLY compact JSON with normalized coordinates:
            {"x":0.50,"y":0.50}
            Do not include markdown or commentary.
            Source file name: {{Path.GetFileName(inputPath)}}
            """;

        var text = await RequestVisionTextAsync(prompt, image, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (TryParseCropFocus(text, out var focus))
        {
            return focus;
        }

        return null;
    }

    public async Task<string?> SuggestFileNameAsync(
        Image<Rgba32> image,
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        var prompt = $"""
            Suggest a concise descriptive file name stem for this image.
            Rules:
            - output only the filename stem, no extension
            - lowercase words separated by hyphens
            - max 6 words
            - no punctuation except hyphen
            Source file name: {Path.GetFileName(inputPath)}
            """;

        var text = await RequestVisionTextAsync(prompt, image, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return NormalizeFileStem(text);
    }

    private async Task<bool> ProbeReachabilityAsync(CancellationToken cancellationToken)
    {
        foreach (var uri in _probeUris)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode
                    || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    return true;
                }
            }
            catch
            {
                // Try next candidate endpoint.
            }
        }

        return false;
    }

    private async Task<string?> RequestVisionTextAsync(
        string prompt,
        Image<Rgba32> image,
        CancellationToken cancellationToken)
    {
        if (!await ProbeReachabilityAsync(cancellationToken))
        {
            return null;
        }

        var imageDataUrl = await CreateDataUrlAsync(image, cancellationToken);

        var payload = new
        {
            model = _model,
            temperature = 0,
            max_tokens = 120,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a local-only image assistant. Reply in the exact format requested."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "image_url", image_url = new { url = imageDataUrl } }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, _chatCompletionsUri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ExtractChatContent(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> CreateDataUrlAsync(Image<Rgba32> image, CancellationToken cancellationToken)
    {
        using var clone = image.Clone();
        await using var memory = new MemoryStream();
        await clone.SaveAsJpegAsync(memory, new JpegEncoder { Quality = 80 }, cancellationToken);
        var base64 = Convert.ToBase64String(memory.ToArray());
        return $"data:image/jpeg;base64,{base64}";
    }

    private static string? ExtractChatContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString()?.Trim();
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var item in content.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    builder.Append(item.GetString());
                    continue;
                }

                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    builder.Append(text.GetString());
                }
            }

            var combined = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(combined) ? null : combined;
        }

        return null;
    }

    private static bool TryParseCropFocus(string raw, out CropFocusPoint focus)
    {
        focus = CropFocusPoint.Center;

        var cleaned = raw.Trim();

        if (cleaned.StartsWith("```") && cleaned.EndsWith("```"))
        {
            cleaned = cleaned.Trim('`').Trim();
            if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[4..].Trim();
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var x = ReadNumber(root, "x") ?? ReadNumber(root, "cx");
            var y = ReadNumber(root, "y") ?? ReadNumber(root, "cy");

            if (x.HasValue && y.HasValue)
            {
                focus = new CropFocusPoint(Math.Clamp(x.Value, 0, 1), Math.Clamp(y.Value, 0, 1));
                return true;
            }
        }
        catch
        {
            // Fall through to regex parse.
        }

        var matches = NumberRegex.Matches(cleaned);
        if (matches.Count >= 2
            && double.TryParse(matches[0].Value, out var xRegex)
            && double.TryParse(matches[1].Value, out var yRegex))
        {
            focus = new CropFocusPoint(Math.Clamp(xRegex, 0, 1), Math.Clamp(yRegex, 0, 1));
            return true;
        }

        return false;
    }

    private static double? ReadNumber(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out var value) => value,
            JsonValueKind.String when double.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static string? NormalizeFileStem(string raw)
    {
        var line = raw
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Trim();

        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        line = line
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);

        var chars = line
            .ToLowerInvariant()
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

        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }

    private static bool IsLoopbackEndpoint(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(uri.Host, out var ip))
        {
            return IPAddress.IsLoopback(ip);
        }

        return false;
    }
}
