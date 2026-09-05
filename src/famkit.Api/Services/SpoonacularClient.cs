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
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Spoonacular:ApiKey is not configured in local.settings.json.");
        }

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

    private record SpoonacularIngredientRef([property: JsonPropertyName("name")] string Name);

    private record SpoonacularFindByIngredientsResult(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("image")] string? Image,
        [property: JsonPropertyName("usedIngredients")] List<SpoonacularIngredientRef> UsedIngredients,
        [property: JsonPropertyName("missedIngredients")] List<SpoonacularIngredientRef> MissedIngredients
    );
}
