using Azure;
using Azure.Data.Tables;
using famkit.Models;

namespace famkit.Services;

public class PantryRepository
{
    private const string TableName = "PantryItems";
    private const string PartitionKey = "family";
    private readonly TableClient _table;

    public PantryRepository(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient(TableName);
        _table.CreateIfNotExists();
    }

    public async Task<List<PantryItem>> GetAllAsync()
    {
        var items = new List<PantryItem>();
        await foreach (var item in _table.QueryAsync<PantryItem>(i => i.PartitionKey == PartitionKey))
        {
            items.Add(item);
        }
        return items.OrderByDescending(i => i.AddedDate).ToList();
    }

    public async Task<PantryItem> AddAsync(PantryItemRequest request)
    {
        var item = new PantryItem
        {
            Name = request.Name,
            Category = request.Category,
            Quantity = request.Quantity,
            Unit = request.Unit,
            Source = request.Source,
        };
        await _table.AddEntityAsync(item);
        return item;
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
