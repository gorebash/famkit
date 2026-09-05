namespace famkit.Models;

public record IdentifiedIngredient(string Name, string? EstimatedQuantity, string? Category);

public record IdentifyResponse(List<IdentifiedIngredient> Ingredients);
