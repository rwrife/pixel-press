using PixelPress.Core;
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

    var operations = RecipeOperationFactory.BuildOperations(recipe.Operations);

    Console.WriteLine($"Recipe: {recipe.Name}");
    Console.WriteLine($"Inputs: {inputPaths.Length}");
    Console.WriteLine($"Output: {Path.GetFullPath(outputArg)}");

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
        if (string.IsNullOrWhiteSpace(key) || i + 1 >= args.Length)
        {
            continue;
        }

        values[key] = args[i + 1];
        i++;
    }

    return values;
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
    Console.WriteLine("pixelpress run --recipe <file|preset-name> --in <file-or-dir> --out <dir>");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  pixelpress run --recipe web-export --in ./raw --out ./web");
    Console.WriteLine("  pixelpress run --recipe ./my-recipe.json --in ./input --out ./output");
}
