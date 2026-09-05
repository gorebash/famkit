using Azure;
using Azure.Data.Tables;
using famkit.Models;

namespace famkit.Services;

public class RecipeRepository
{
    private const string TableName = "Recipes";
    private const string PartitionKey = "family";
    private readonly TableClient _table;

    public RecipeRepository(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient(TableName);
        _table.CreateIfNotExists();
    }

    public async Task<List<Recipe>> GetAllAsync()
    {
        var items = new List<Recipe>();
        await foreach (var item in _table.QueryAsync<Recipe>(i => i.PartitionKey == PartitionKey))
        {
            items.Add(item);
        }
        return items.OrderByDescending(i => i.CreatedDate).ToList();
    }

    public async Task<Recipe?> GetAsync(string id)
    {
        try
        {
            return await _table.GetEntityAsync<Recipe>(PartitionKey, id);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<Recipe> AddAsync(RecipeRequest request)
    {
        var recipe = new Recipe
        {
            Title = request.Title,
            Instructions = request.Instructions,
            SourceUrl = request.SourceUrl,
            ImageUrl = request.ImageUrl,
            MealType = MealTypes.IsValid(request.MealType) ? request.MealType! : MealTypes.Any,
            PrepTime = PrepTimes.IsValid(request.PrepTime) ? request.PrepTime! : PrepTimes.Standard,
            SpoonacularId = request.SpoonacularId,
        };
        recipe.SetIngredients(request.Ingredients);
        await _table.AddEntityAsync(recipe);
        return recipe;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        try
        {
            await _table.DeleteEntityAsync(PartitionKey, id);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
