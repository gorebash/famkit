using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace famkit.Models;

public record RecipeIngredient(string Name, string? Quantity, string? Unit);

/// <summary>Broad meal-type bucket. Kept intentionally small — no snacks/desserts/etc.</summary>
public static class MealTypes
{
    public const string Breakfast = "breakfast";
    public const string Lunch = "lunch";
    public const string Dinner = "dinner";
    public const string Any = "any";

    public static bool IsValid(string? value) =>
        value is Breakfast or Lunch or Dinner or Any;
}

/// <summary>Two-bucket prep-time indicator. Quick ≈ 30 min or less.</summary>
public static class PrepTimes
{
    public const string Quick = "quick";
    public const string Standard = "standard";

    public static bool IsValid(string? value) =>
        value is Quick or Standard;

    public static string FromMinutes(int? minutes) =>
        minutes is > 0 and <= 30 ? Quick : Standard;
}

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
    public string? ImageUrl { get; set; }
    public string MealType { get; set; } = MealTypes.Any;
    public string PrepTime { get; set; } = PrepTimes.Standard;
    public int? SpoonacularId { get; set; }
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    public List<RecipeIngredient> GetIngredients() =>
        JsonSerializer.Deserialize<List<RecipeIngredient>>(IngredientsJson) ?? new();

    public void SetIngredients(List<RecipeIngredient> ingredients) =>
        IngredientsJson = JsonSerializer.Serialize(ingredients);
}

public record RecipeRequest(
    string Title,
    List<RecipeIngredient> Ingredients,
    string? Instructions,
    string? SourceUrl,
    string? ImageUrl = null,
    string? MealType = null,
    string? PrepTime = null,
    int? SpoonacularId = null);

public record IngredientDiffResult(List<string> Have, List<string> Missing);
