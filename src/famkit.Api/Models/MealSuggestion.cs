namespace famkit.Models;

public record MealSuggestion(
    int SpoonacularId,
    string Title,
    string? ImageUrl,
    List<string> UsedIngredients,
    List<string> MissedIngredients
);

public record MealPlanResponse(List<MealSuggestion> Suggestions, List<string> GroceryList);
