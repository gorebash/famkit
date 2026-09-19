namespace famkit.McpTools;

/// <summary>
/// Names and descriptions for the FamKit MCP tools.
/// These strings are visible to MCP clients (Claude Desktop, VS Code Copilot, MCP Inspector),
/// so keep them clear enough that an LLM can pick the right tool from context alone.
/// </summary>
internal static class McpToolsInformation
{
    // list_pantry
    public const string ListPantryToolName = "list_pantry";
    public const string ListPantryToolDescription =
        "Returns every item currently in the family's pantry and fridge. No arguments. Use this when you need to know what ingredients are on hand.";

    // add_pantry_item
    public const string AddPantryItemToolName = "add_pantry_item";
    public const string AddPantryItemToolDescription =
        "Adds a single ingredient to the family's pantry or fridge.";
    public const string AddPantryItemNamePropertyName = "name";
    public const string AddPantryItemNamePropertyDescription =
        "The ingredient's common name, e.g. 'milk', 'cheddar cheese', 'eggs'. Lowercase, singular form preferred.";
    public const string AddPantryItemCategoryPropertyName = "category";
    public const string AddPantryItemCategoryPropertyDescription =
        "Where the item is stored: 'fridge' or 'pantry'. Defaults to 'pantry' if omitted.";
    public const string AddPantryItemQuantityPropertyName = "quantity";
    public const string AddPantryItemQuantityPropertyDescription =
        "Optional free-text quantity, e.g. '1 dozen', 'half gallon', '2 cans'.";

    // list_recipes
    public const string ListRecipesToolName = "list_recipes";
    public const string ListRecipesToolDescription =
        "Returns saved family recipes, optionally filtered by meal type and prep time.";
    public const string ListRecipesMealTypePropertyName = "meal_type";
    public const string ListRecipesMealTypePropertyDescription =
        "Optional filter: 'breakfast', 'lunch', 'dinner', or 'any'. Omit for no filter.";
    public const string ListRecipesPrepTimePropertyName = "prep_time";
    public const string ListRecipesPrepTimePropertyDescription =
        "Optional filter: 'quick' (30 minutes or less) or 'standard' (longer). Omit for no filter.";

    // evaluate_recipe_ingredients
    public const string EvaluateRecipeIngredientsToolName = "evaluate_recipe_ingredients";
    public const string EvaluateRecipeIngredientsToolDescription =
        "Compares a list of ingredient names against the current pantry and returns which ones are on hand versus missing. Use this to build a grocery list for a specific recipe.";
    public const string EvaluateRecipeIngredientsPropertyName = "ingredients";
    public const string EvaluateRecipeIngredientsPropertyDescription =
        "Array of ingredient names to check, e.g. ['flour', 'sugar', 'eggs', 'milk'].";
}
