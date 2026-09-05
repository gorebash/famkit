using famkit.Models;

namespace famkit.Services;

public static class IngredientMatcher
{
    /// <summary>
    /// Diffs a recipe's required ingredient names against what's currently in the pantry.
    /// Matching is case-insensitive and tolerant of one name containing the other
    /// (e.g. pantry "chicken breast" satisfies recipe "chicken").
    /// </summary>
    public static IngredientDiffResult Diff(IEnumerable<string> requiredIngredientNames, IEnumerable<PantryItem> pantryItems)
    {
        var pantryNames = pantryItems
            .Select(p => p.Name.Trim().ToLowerInvariant())
            .ToList();

        var have = new List<string>();
        var missing = new List<string>();

        foreach (var required in requiredIngredientNames)
        {
            var normalized = required.Trim().ToLowerInvariant();
            var isInPantry = pantryNames.Any(p => p.Contains(normalized) || normalized.Contains(p));

            if (isInPantry)
            {
                have.Add(required);
            }
            else
            {
                missing.Add(required);
            }
        }

        return new IngredientDiffResult(have, missing);
    }
}
