using PixelPress.Core;
using PixelPress.Core.Ai;
using PixelPress.Core.Imaging;
using PixelPress.Core.Recipes;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || HasFlag(args, "-h") || HasFlag(args, "--help"))
    {
        PrintHelp();
        return 0;
    }

    if (!string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Only the 'run' command is supported.");
        PrintHelp();
        return 2;
    }

    var values = ParseOptions(args.Skip(1).ToArray());

    if (!values.TryGetValue("recipe", out var recipeArg)
        || !values.TryGetValue("in", out var inputArg)
        || !values.TryGetValue("out", out var outputArg))
    {
        Console.Error.WriteLine("Missing required arguments. Expected --recipe, --in, and --out.");
        PrintHelp();
        return 2;
    }

    var inputPaths = DiscoverInputFiles(inputArg).ToArray();
    if (inputPaths.Length == 0)
    {
        Console.Error.WriteLine($"No supported image files were found under '{inputArg}'.");
        return 2;
    }

    Directory.CreateDirectory(outputArg);

    var recipeStore = new RecipeStore();
    recipeStore.EnsureBuiltInRecipes();

    PipelineRecipe recipe;
    try
    {
        recipe = LoadRecipe(recipeStore, recipeArg);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to load recipe '{recipeArg}': {ex.Message}");
        return 2;
    }

    var enableAiSmartCrop = OptionEnabled(values, "ai-smart-crop");
    var enableAiAutoName = OptionEnabled(values, "ai-auto-name");

    IImageAiService? aiService = null;
    if (enableAiSmartCrop || enableAiAutoName)
    {
        aiService = await BuildAiServiceAsync(values);
        if (aiService is null)
        {
            Console.WriteLine("AI endpoint unavailable or invalid; continuing with deterministic fallback behavior.");
        }
    }

    var operations = RecipeOperationFactory.BuildOperations(
        recipe.Operations,
        aiService,
        enableSmartCrop: enableAiSmartCrop,
        enableAutoNaming: enableAiAutoName);

    Console.WriteLine($"Recipe: {recipe.Name}");
    Console.WriteLine($"Inputs: {inputPaths.Length}");
    Console.WriteLine($"Output: {Path.GetFullPath(outputArg)}");

    if (enableAiSmartCrop || enableAiAutoName)
    {
        Console.WriteLine($"AI smart crop: {(enableAiSmartCrop ? "enabled" : "disabled")}");
        Console.WriteLine($"AI auto naming: {(enableAiAutoName ? "enabled" : "disabled")}");
    }

    var runner = new PipelineRunner(new ImageSharpImageProcessor());

    var result = await runner.RunAsync(
        new PipelineRunRequest
        {
            InputPaths = inputPaths,
            OutputDirectory = outputArg,
            Operations = operations,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
            OverwriteExisting = true
        },
        progress =>
        {
            var fileName = Path.GetFileName(progress.InputPath);
            Console.WriteLine($"[{progress.Completed}/{progress.Total}] {progress.Status}: {fileName}");
        });

    PrintSummary(result);

    return result.Failed > 0 ? 1 : 0;
}

static async Task<IImageAiService?> BuildAiServiceAsync(IReadOnlyDictionary<string, string> values)
{
    if (!values.TryGetValue("ai-endpoint", out var endpointRaw)
        || string.IsNullOrWhiteSpace(endpointRaw))
    {
        Console.WriteLine("AI features were requested, but --ai-endpoint was not provided.");
        return null;
    }

    if (!LocalOpenAiImageAiService.TryResolveLoopbackEndpoint(endpointRaw, out var endpoint, out var failureReason))
    {
        Console.WriteLine($"AI endpoint rejected: {failureReason}");
        return null;
    }

    var model = values.TryGetValue("ai-model", out var modelRaw) && !string.IsNullOrWhiteSpace(modelRaw)
        ? modelRaw
        : "minicpm-v";

    var apiKey = values.TryGetValue("ai-api-key", out var apiKeyRaw) && !string.IsNullOrWhiteSpace(apiKeyRaw)
        ? apiKeyRaw
        : null;

    var service = new LocalOpenAiImageAiService(endpoint!, model, apiKey);
    var reachable = await service.IsReachableAsync();
    if (!reachable)
    {
        Console.WriteLine($"AI endpoint is not reachable at {endpoint}.");
        return null;
    }

    Console.WriteLine($"AI endpoint reachable: {endpoint} (model: {model})");
    return service;
}

static PipelineRecipe LoadRecipe(RecipeStore store, string recipeArg)
{
    if (File.Exists(recipeArg))
    {
        return store.Load(recipeArg);
    }

    var builtIn = RecipeCatalog.FindBuiltIn(recipeArg);
    if (builtIn is not null)
    {
        return builtIn;
    }

    return store.Load(recipeArg);
}

static IEnumerable<string> DiscoverInputFiles(string inputArg)
{
    var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tif", ".tiff", ".heic"
    };

    if (File.Exists(inputArg))
    {
        var extension = Path.GetExtension(inputArg);
        if (supportedExtensions.Contains(extension))
        {
            yield return Path.GetFullPath(inputArg);
        }

        yield break;
    }

    if (!Directory.Exists(inputArg))
    {
        yield break;
    }

    foreach (var file in Directory.EnumerateFiles(inputArg, "*.*", SearchOption.AllDirectories))
    {
        if (supportedExtensions.Contains(Path.GetExtension(file)))
        {
            yield return Path.GetFullPath(file);
        }
    }
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = arg[2..];
        if (string.IsNullOrWhiteSpace(key))
        {
            continue;
        }

        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            values[key] = args[i + 1];
            i++;
            continue;
        }

        values[key] = "true";
    }

    return values;
}

static bool OptionEnabled(IReadOnlyDictionary<string, string> values, string key)
{
    if (!values.TryGetValue(key, out var raw))
    {
        return false;
    }

    if (string.IsNullOrWhiteSpace(raw))
    {
        return true;
    }

    return raw.Equals("true", StringComparison.OrdinalIgnoreCase)
        || raw.Equals("1", StringComparison.OrdinalIgnoreCase)
        || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || raw.Equals("on", StringComparison.OrdinalIgnoreCase);
}

static bool HasFlag(IEnumerable<string> args, string flag)
    => args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));

static void PrintSummary(BatchResult result)
{
    var inputBytes = result.Files.Sum(file => file.InputBytes);
    var outputBytes = result.Files.Where(file => file.Status == FileProcessStatus.Success).Sum(file => file.OutputBytes);

    Console.WriteLine();
    Console.WriteLine("Summary");
    Console.WriteLine($"  Processed: {result.TotalFiles}");
    Console.WriteLine($"  Succeeded: {result.Succeeded}");
    Console.WriteLine($"  Skipped:   {result.Skipped}");
    Console.WriteLine($"  Failed:    {result.Failed}");
    Console.WriteLine($"  Elapsed:   {result.Elapsed:g}");
    Console.WriteLine($"  Input:     {FormatBytes(inputBytes)}");
    Console.WriteLine($"  Output:    {FormatBytes(outputBytes)}");
}

static string FormatBytes(long bytes)
{
    var value = Math.Abs((double)bytes);
    var units = new[] { "B", "KB", "MB", "GB" };
    var unitIndex = 0;

    while (value >= 1024 && unitIndex < units.Length - 1)
    {
        value /= 1024;
        unitIndex++;
    }

    return $"{value:0.##} {units[unitIndex]}";
}

static void PrintHelp()
{
    Console.WriteLine("pixelpress run --recipe <file|preset-name> --in <file-or-dir> --out <dir> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --ai-smart-crop               Enable AI-assisted crop focus for Fill resize operations");
    Console.WriteLine("  --ai-auto-name                Enable AI-assisted output naming for Rename operations");
    Console.WriteLine("  --ai-endpoint <url>           Local OpenAI-compatible endpoint (loopback only)");
    Console.WriteLine("  --ai-model <name>             Model name (default: minicpm-v)");
    Console.WriteLine("  --ai-api-key <token>          Optional API key for local endpoint");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  pixelpress run --recipe web-export --in ./raw --out ./web");
    Console.WriteLine("  pixelpress run --recipe thumbnails --in ./raw --out ./thumbs --ai-smart-crop --ai-endpoint http://127.0.0.1:11434/v1 --ai-model minicpm-v");
    Console.WriteLine("  pixelpress run --recipe web-export --in ./raw --out ./web --ai-auto-name --ai-endpoint http://127.0.0.1:11434/v1 --ai-model minicpm-v");
}
