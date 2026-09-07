using famkit.Models;
using famkit.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using static famkit.McpTools.McpToolsInformation;

namespace famkit.McpTools;

/// <summary>
/// MCP tools for the recipe library and the pantry diff. Uses the same repositories and
/// ingredient matcher as the HTTP API so the behaviour is identical across surfaces.
/// </summary>
public class RecipeMcpTools(
    RecipeRepository recipeRepository,
    PantryRepository pantryRepository,
    ILogger<RecipeMcpTools> logger)
{
    [Function(nameof(McpListRecipes))]
    public async Task<IReadOnlyList<object>> McpListRecipes(
        [McpToolTrigger(ListRecipesToolName, ListRecipesToolDescription)] ToolInvocationContext context,
        [McpToolProperty(ListRecipesMealTypePropertyName, ListRecipesMealTypePropertyDescription, false)] string? mealType,
        [McpToolProperty(ListRecipesPrepTimePropertyName, ListRecipesPrepTimePropertyDescription, false)] string? prepTime)
    {
        logger.LogInformation("MCP tool '{Tool}' invoked with mealType={MealType}, prepTime={PrepTime}.", context.Name, mealType, prepTime);

        var recipes = await recipeRepository.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(mealType) && MealTypes.IsValid(mealType))
        {
            recipes = recipes.Where(r => r.MealType == mealType).ToList();
        }
        if (!string.IsNullOrWhiteSpace(prepTime) && PrepTimes.IsValid(prepTime))
        {
            recipes = recipes.Where(r => r.PrepTime == prepTime).ToList();
        }

        return recipes.Select(r => new
        {
            r.Title,
            r.MealType,
            r.PrepTime,
            Ingredients = r.GetIngredients().Select(i => i.Name).ToList(),
            r.SourceUrl,
        }).ToList();
    }

    [Function(nameof(McpEvaluateRecipeIngredients))]
    public async Task<object> McpEvaluateRecipeIngredients(
        [McpToolTrigger(EvaluateRecipeIngredientsToolName, EvaluateRecipeIngredientsToolDescription)] ToolInvocationContext context,
        [McpToolProperty(EvaluateRecipeIngredientsPropertyName, EvaluateRecipeIngredientsPropertyDescription, true)] IEnumerable<string> ingredients)
    {
        var ingredientList = ingredients.ToList();
        logger.LogInformation("MCP tool '{Tool}' invoked with {Count} ingredients.", context.Name, ingredientList.Count);

        var pantryItems = await pantryRepository.GetAllAsync();
        var diff = IngredientMatcher.Diff(ingredientList, pantryItems);

        return new
        {
            have = diff.Have,
            missing = diff.Missing,
            summary = diff.Missing.Count == 0
                ? "You have everything you need."
                : $"You need to buy {diff.Missing.Count} item(s): {string.Join(", ", diff.Missing)}.",
        };
    }
}
