using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelPress.Core.Recipes;

public static class RecipeJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(PipelineRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return JsonSerializer.Serialize(recipe, JsonOptions);
    }

    public static PipelineRecipe Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var recipe = JsonSerializer.Deserialize<PipelineRecipe>(json, JsonOptions);
        if (recipe is null)
        {
            throw new InvalidOperationException("Recipe JSON could not be deserialized.");
        }

        recipe.Name = string.IsNullOrWhiteSpace(recipe.Name)
            ? "Untitled Recipe"
            : recipe.Name.Trim();
        recipe.Operations ??= [];

        return recipe;
    }
}
