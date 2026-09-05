using famkit.Models;
using famkit.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace famkit.Functions;

public record EvaluateRecipeRequest(string? RecipeId, List<RecipeIngredient>? Ingredients);

public class RecipeFunctions(RecipeRepository recipeRepository, PantryRepository pantryRepository)
{
    [Function("GetRecipes")]
    public async Task<IActionResult> GetRecipes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "recipes")] HttpRequest req)
    {
        var recipes = await recipeRepository.GetAllAsync();
        return new OkObjectResult(recipes.Select(ToDto));
    }

    [Function("AddRecipe")]
    public async Task<IActionResult> AddRecipe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "recipes")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<RecipeRequest>();
        if (request is null || string.IsNullOrWhiteSpace(request.Title))
        {
            return new BadRequestObjectResult("Title is required.");
        }

        var recipe = await recipeRepository.AddAsync(request);
        return new OkObjectResult(ToDto(recipe));
    }

    [Function("DeleteRecipe")]
    public async Task<IActionResult> DeleteRecipe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "recipes/{id}")] HttpRequest req,
        string id)
    {
        var deleted = await recipeRepository.DeleteAsync(id);
        return deleted ? new NoContentResult() : new NotFoundResult();
    }

    [Function("EvaluateRecipe")]
    public async Task<IActionResult> EvaluateRecipe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "recipes/evaluate")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<EvaluateRecipeRequest>();
        if (request is null)
        {
            return new BadRequestObjectResult("Request body is required.");
        }

        List<RecipeIngredient> ingredients;
        if (!string.IsNullOrWhiteSpace(request.RecipeId))
        {
            var recipe = await recipeRepository.GetAsync(request.RecipeId);
            if (recipe is null)
            {
                return new NotFoundObjectResult($"Recipe '{request.RecipeId}' not found.");
            }
            ingredients = recipe.GetIngredients();
        }
        else if (request.Ingredients is { Count: > 0 })
        {
            ingredients = request.Ingredients;
        }
        else
        {
            return new BadRequestObjectResult("Either recipeId or ingredients must be provided.");
        }

        var pantryItems = await pantryRepository.GetAllAsync();
        var diff = IngredientMatcher.Diff(ingredients.Select(i => i.Name), pantryItems);
        return new OkObjectResult(diff);
    }

    private static object ToDto(Recipe recipe) => new
    {
        id = recipe.RowKey,
        recipe.Title,
        Ingredients = recipe.GetIngredients(),
        recipe.Instructions,
        recipe.SourceUrl,
        recipe.CreatedDate,
    };
}
