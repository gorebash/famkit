using Azure;
using Azure.Data.Tables;

namespace famkit.Models;

public class PantryItem : ITableEntity
{
    public string PartitionKey { get; set; } = "family";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "pantry"; // "pantry" or "fridge"
    public string? Quantity { get; set; }
    public string? Unit { get; set; }
    public string Source { get; set; } = "manual"; // "manual" or "photo"
    public DateTimeOffset AddedDate { get; set; } = DateTimeOffset.UtcNow;
}

public record PantryItemRequest(string Name, string Category, string? Quantity, string? Unit, string Source = "manual");
