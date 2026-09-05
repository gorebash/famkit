using System.Net.Http.Json;
using System.Text.Json.Serialization;
using famkit.Models;
using Microsoft.Extensions.Configuration;

namespace famkit.Services;

public class SpoonacularClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public SpoonacularClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= new Uri("https://api.spoonacular.com/");
        _apiKey = configuration["Spoonacular:ApiKey"];
    }

    public async Task<List<MealSuggestion>> FindByIngredientsAsync(IEnumerable<string> ingredientNames, int number = 6, CancellationToken cancellationToken = default)
    {
        RequireApiKey();

        var ingredients = string.Join(',', ingredientNames);
        var url = $"recipes/findByIngredients?ingredients={Uri.EscapeDataString(ingredients)}&number={number}&ranking=2&ignorePantry=true&apiKey={_apiKey}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<SpoonacularFindByIngredientsResult>>(cancellationToken: cancellationToken) ?? [];

        return results.Select(r => new MealSuggestion(
            r.Id,
            r.Title,
            r.Image,
            r.UsedIngredients.Select(i => i.Name).ToList(),
            r.MissedIngredients.Select(i => i.Name).ToList()
        )).ToList();
    }

    /// <summary>Fetches full recipe details (ingredients, instructions, timing, dish types) and shapes them into a RecipeRequest ready to persist.</summary>
    public async Task<RecipeRequest> GetRecipeAsRequestAsync(int spoonacularId, CancellationToken cancellationToken = default)
    {
        RequireApiKey();

        var url = $"recipes/{spoonacularId}/information?includeNutrition=false&apiKey={_apiKey}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var info = await response.Content.ReadFromJsonAsync<SpoonacularRecipeInformation>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Spoonacular returned no data for recipe {spoonacularId}.");

        var ingredients = (info.ExtendedIngredients ?? [])
            .Select(i => new RecipeIngredient(
                i.NameClean ?? i.Name ?? "unknown",
                i.Amount is > 0 ? i.Amount.ToString() : null,
                string.IsNullOrWhiteSpace(i.Unit) ? null : i.Unit))
            .ToList();

        return new RecipeRequest(
            Title: info.Title ?? $"Recipe {spoonacularId}",
            Ingredients: ingredients,
            Instructions: info.Instructions,
            SourceUrl: info.SourceUrl,
            ImageUrl: info.Image,
            MealType: InferMealType(info.DishTypes),
            PrepTime: PrepTimes.FromMinutes(info.ReadyInMinutes),
            SpoonacularId: spoonacularId);
    }

    private void RequireApiKey()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Spoonacular:ApiKey is not configured in local.settings.json.");
        }
    }

    /// <summary>Maps Spoonacular's dishTypes[] onto our four coarse buckets. Prefers the most specific match.</summary>
    private static string InferMealType(List<string>? dishTypes)
    {
        if (dishTypes is null || dishTypes.Count == 0) return MealTypes.Any;
        var set = dishTypes.Select(d => d.ToLowerInvariant()).ToHashSet();

        if (set.Contains("breakfast") || set.Contains("morning meal") || set.Contains("brunch")) return MealTypes.Breakfast;
        if (set.Contains("lunch")) return MealTypes.Lunch;
        if (set.Contains("dinner") || set.Contains("main course") || set.Contains("main dish")) return MealTypes.Dinner;
        return MealTypes.Any;
    }

    private record SpoonacularIngredientRef([property: JsonPropertyName("name")] string Name);

    private record SpoonacularFindByIngredientsResult(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("image")] string? Image,
        [property: JsonPropertyName("usedIngredients")] List<SpoonacularIngredientRef> UsedIngredients,
        [property: JsonPropertyName("missedIngredients")] List<SpoonacularIngredientRef> MissedIngredients
    );

    private record SpoonacularExtendedIngredient(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("nameClean")] string? NameClean,
        [property: JsonPropertyName("amount")] double? Amount,
        [property: JsonPropertyName("unit")] string? Unit
    );

    private record SpoonacularRecipeInformation(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("image")] string? Image,
        [property: JsonPropertyName("sourceUrl")] string? SourceUrl,
        [property: JsonPropertyName("readyInMinutes")] int? ReadyInMinutes,
        [property: JsonPropertyName("instructions")] string? Instructions,
        [property: JsonPropertyName("dishTypes")] List<string>? DishTypes,
        [property: JsonPropertyName("extendedIngredients")] List<SpoonacularExtendedIngredient>? ExtendedIngredients
    );
}
