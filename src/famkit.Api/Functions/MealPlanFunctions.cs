using famkit.Models;
using famkit.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace famkit.Functions;

public class MealPlanFunctions(PantryRepository pantryRepository, SpoonacularClient spoonacularClient, ILogger<MealPlanFunctions> logger)
{
    [Function("SuggestMealPlan")]
    public async Task<IActionResult> SuggestMealPlan(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mealplan/suggest")] HttpRequest req)
    {
        var pantryItems = await pantryRepository.GetAllAsync();
        if (pantryItems.Count == 0)
        {
            return new BadRequestObjectResult("Pantry is empty — add some ingredients first.");
        }

        try
        {
            var suggestions = await spoonacularClient.FindByIngredientsAsync(pantryItems.Select(p => p.Name));
            var groceryList = suggestions
                .SelectMany(s => s.MissedIngredients)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(i => i)
                .ToList();

            return new OkObjectResult(new MealPlanResponse(suggestions, groceryList));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Spoonacular is not configured.");
            return new ObjectResult(ex.Message) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
    }
}
