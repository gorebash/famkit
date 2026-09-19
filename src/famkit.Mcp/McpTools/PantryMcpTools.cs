using famkit.Models;
using famkit.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using static famkit.McpTools.McpToolsInformation;

namespace famkit.McpTools;

/// <summary>
/// MCP tools for pantry/fridge inventory. Reuses the same PantryRepository as the HTTP API,
/// so the same Azure Table Storage state is visible to both surfaces.
/// </summary>
public class PantryMcpTools(PantryRepository pantryRepository, ILogger<PantryMcpTools> logger)
{
    [Function(nameof(McpListPantry))]
    public async Task<IReadOnlyList<object>> McpListPantry(
        [McpToolTrigger(ListPantryToolName, ListPantryToolDescription)] ToolInvocationContext context)
    {
        logger.LogInformation("MCP tool '{Tool}' invoked.", context.Name);
        var items = await pantryRepository.GetAllAsync();
        return items.Select(i => new
        {
            i.Name,
            i.Category,
            i.Quantity,
            AddedDate = i.AddedDate.ToString("O"),
        }).ToList();
    }

    [Function(nameof(McpAddPantryItem))]
    public async Task<object> McpAddPantryItem(
        [McpToolTrigger(AddPantryItemToolName, AddPantryItemToolDescription)] ToolInvocationContext context,
        [McpToolProperty(AddPantryItemNamePropertyName, AddPantryItemNamePropertyDescription, true)] string name,
        [McpToolProperty(AddPantryItemCategoryPropertyName, AddPantryItemCategoryPropertyDescription, false)] string? category,
        [McpToolProperty(AddPantryItemQuantityPropertyName, AddPantryItemQuantityPropertyDescription, false)] string? quantity)
    {
        logger.LogInformation("MCP tool '{Tool}' invoked with name={Name}, category={Category}.", context.Name, name, category);

        var normalizedCategory = category?.Trim().ToLowerInvariant() switch
        {
            "fridge" => "fridge",
            "pantry" => "pantry",
            _ => "pantry",
        };

        var request = new PantryItemRequest(
            Name: name.Trim(),
            Category: normalizedCategory,
            Quantity: string.IsNullOrWhiteSpace(quantity) ? null : quantity.Trim(),
            Unit: null,
            Source: "mcp");

        var item = await pantryRepository.AddAsync(request);
        return new
        {
            message = $"Added '{item.Name}' to {item.Category}.",
            item = new { item.Name, item.Category, item.Quantity },
        };
    }
}
