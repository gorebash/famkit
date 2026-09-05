using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace famkit.Models;

public record RecipeIngredient(string Name, string? Quantity, string? Unit);

public class Recipe : ITableEntity
{
    public string PartitionKey { get; set; } = "family";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Title { get; set; } = string.Empty;
    public string IngredientsJson { get; set; } = "[]";
    public string? Instructions { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    public List<RecipeIngredient> GetIngredients() =>
        JsonSerializer.Deserialize<List<RecipeIngredient>>(IngredientsJson) ?? new();

    public void SetIngredients(List<RecipeIngredient> ingredients) =>
        IngredientsJson = JsonSerializer.Serialize(ingredients);
}

public record RecipeRequest(string Title, List<RecipeIngredient> Ingredients, string? Instructions, string? SourceUrl);

public record IngredientDiffResult(List<string> Have, List<string> Missing);
