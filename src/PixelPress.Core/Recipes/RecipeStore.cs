namespace PixelPress.Core.Recipes;

public sealed class RecipeStore
{
    public RecipeStore(string? recipesDirectory = null)
    {
        RecipesDirectory = recipesDirectory ?? GetDefaultRecipesDirectory();
    }

    public string RecipesDirectory { get; }

    public string Save(PipelineRecipe recipe, string? explicitPath = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        Directory.CreateDirectory(RecipesDirectory);

        var targetPath = explicitPath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            var fileName = $"{Slugify(recipe.Name)}.json";
            targetPath = Path.Combine(RecipesDirectory, fileName);
        }

        var directory = Path.GetDirectoryName(targetPath) ?? RecipesDirectory;
        Directory.CreateDirectory(directory);

        File.WriteAllText(targetPath, RecipeJson.Serialize(recipe));
        return targetPath;
    }

    public PipelineRecipe Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = ResolveRecipePath(path);
        var json = File.ReadAllText(fullPath);
        return RecipeJson.Deserialize(json);
    }

    public IReadOnlyList<string> ListRecipeFiles()
    {
        if (!Directory.Exists(RecipesDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(RecipesDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void EnsureBuiltInRecipes()
    {
        Directory.CreateDirectory(RecipesDirectory);

        foreach (var preset in RecipeCatalog.BuiltInPresets)
        {
            var fileName = $"{Slugify(preset.Name)}.json";
            var path = Path.Combine(RecipesDirectory, fileName);

            if (!File.Exists(path))
            {
                Save(preset, path);
            }
        }
    }

    private string ResolveRecipePath(string recipeArg)
    {
        if (File.Exists(recipeArg))
        {
            return Path.GetFullPath(recipeArg);
        }

        var withExtension = recipeArg.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? recipeArg
            : $"{recipeArg}.json";

        var localPath = Path.Combine(RecipesDirectory, withExtension);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        var slugPath = Path.Combine(RecipesDirectory, $"{Slugify(recipeArg)}.json");
        if (File.Exists(slugPath))
        {
            return slugPath;
        }

        throw new FileNotFoundException($"Recipe not found: {recipeArg}");
    }

    public static string GetDefaultRecipesDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "pixel-press", "recipes");
    }

    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "recipe";
        }

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
        return string.IsNullOrWhiteSpace(slug) ? "recipe" : slug;
    }
}
