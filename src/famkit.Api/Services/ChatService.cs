using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using famkit.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace famkit.Services;

public record ChatMessageDto(string Role, string Content);
public record ChatRequestDto(List<ChatMessageDto> Messages);
public record ChatResponseDto(string Reply, List<string> ToolActivity);

/// <summary>
/// Handles conversational chat with tool calling. The model is Llama-4-Scout on Foundry
/// (same deployment as vision), and the tools it can call are a small set that mirror
/// the MCP tools we expose to external clients — but here they invoke the repositories
/// directly rather than going through the MCP HTTP endpoint. That keeps this handler
/// dependency-free from MCP session/SSE plumbing while still teaching function calling.
/// </summary>
public class ChatService
{
    private const int MaxToolIterations = 5;

    private const string SystemPrompt =
        "You are FamKit, a friendly kitchen assistant for one family. You can see and modify the family's pantry, " +
        "fridge, and saved-recipe library through the tools available to you.\n\n" +
        "Use tools whenever the user asks a question that depends on real inventory or saved recipes — never guess " +
        "at what's in the fridge. When the user mentions cooking or preparing a specific dish (e.g. 'I'm making " +
        "tacos'), use your general cooking knowledge to list typical ingredients for that dish, then call " +
        "evaluate_recipe_ingredients to check which are on hand and which the user needs to buy.\n\n" +
        "When adding items on the user's behalf, confirm what you added. Keep replies concise, warm, and " +
        "helpful — one or two short paragraphs at most.";

    private readonly ChatClient? _chatClient;
    private readonly ILogger<ChatService> _logger;
    private readonly PantryRepository _pantryRepository;
    private readonly RecipeRepository _recipeRepository;

    public ChatService(
        IConfiguration configuration,
        ILogger<ChatService> logger,
        PantryRepository pantryRepository,
        RecipeRepository recipeRepository)
    {
        _logger = logger;
        _pantryRepository = pantryRepository;
        _recipeRepository = recipeRepository;

        var endpoint = configuration["Foundry:Endpoint"];
        var apiKey = configuration["Foundry:ApiKey"];
        // Chat uses its own deployment (a tool-calling-capable model) separate from the vision deployment.
        var deploymentName = configuration["Foundry:ChatDeploymentName"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _logger.LogWarning("Foundry chat configuration is incomplete; chat will fail until Foundry:Endpoint, Foundry:ApiKey and Foundry:ChatDeploymentName are set.");
            return;
        }

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = azureClient.GetChatClient(deploymentName);
    }

    public async Task<ChatResponseDto> ChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default)
    {
        if (_chatClient is null)
        {
            throw new InvalidOperationException("Foundry chat client is not configured.");
        }

        var messages = new List<ChatMessage> { new SystemChatMessage(SystemPrompt) };
        foreach (var m in request.Messages)
        {
            messages.Add(m.Role switch
            {
                "user" => new UserChatMessage(m.Content),
                "assistant" => new AssistantChatMessage(m.Content),
                _ => new UserChatMessage(m.Content),
            });
        }

        var options = new ChatCompletionOptions();
        foreach (var tool in ChatTools) options.Tools.Add(tool);

        var toolActivity = new List<string>();

        for (int iter = 0; iter < MaxToolIterations; iter++)
        {
            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);

            if (completion.FinishReason != ChatFinishReason.ToolCalls)
            {
                var reply = completion.Content.Count > 0 ? completion.Content[0].Text : "";
                return new ChatResponseDto(reply, toolActivity);
            }

            // Preserve the assistant's tool-call turn in the message list before we add tool results.
            messages.Add(new AssistantChatMessage(completion));

            foreach (var toolCall in completion.ToolCalls)
            {
                var (result, activityLine) = await InvokeToolAsync(toolCall, cancellationToken);
                toolActivity.Add(activityLine);
                messages.Add(new ToolChatMessage(toolCall.Id, result));
            }
        }

        _logger.LogWarning("Chat loop hit MaxToolIterations={Max} without a final response.", MaxToolIterations);
        return new ChatResponseDto(
            "I got tangled up trying to look that up. Could you rephrase?",
            toolActivity);
    }

    private async Task<(string result, string activity)> InvokeToolAsync(ChatToolCall toolCall, CancellationToken cancellationToken)
    {
        var name = toolCall.FunctionName;
        var argsJson = toolCall.FunctionArguments.ToString();
        _logger.LogInformation("Chat tool call: {Tool} with args {Args}", name, argsJson);

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
            var args = doc.RootElement;

            switch (name)
            {
                case "list_pantry":
                {
                    var items = await _pantryRepository.GetAllAsync();
                    var summary = items.Select(i => new { i.Name, i.Category, i.Quantity }).ToList();
                    return (JsonSerializer.Serialize(summary), $"Looked up pantry ({items.Count} items).");
                }
                case "add_pantry_item":
                {
                    var itemName = args.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var category = args.TryGetProperty("category", out var c) ? c.GetString() : null;
                    var quantity = args.TryGetProperty("quantity", out var q) ? q.GetString() : null;

                    if (string.IsNullOrWhiteSpace(itemName))
                    {
                        return ("{\"error\":\"name is required\"}", $"Tried to add a pantry item without a name.");
                    }

                    var normalizedCategory = category?.Trim().ToLowerInvariant() switch
                    {
                        "fridge" => "fridge",
                        "pantry" => "pantry",
                        _ => "pantry",
                    };

                    var added = await _pantryRepository.AddAsync(new PantryItemRequest(
                        itemName.Trim(), normalizedCategory, quantity, null, "chat"));

                    return (
                        JsonSerializer.Serialize(new { added.Name, added.Category, added.Quantity }),
                        $"Added '{added.Name}' to {added.Category}."
                    );
                }
                case "list_recipes":
                {
                    var mealType = args.TryGetProperty("meal_type", out var mt) ? mt.GetString() : null;
                    var prepTime = args.TryGetProperty("prep_time", out var pt) ? pt.GetString() : null;

                    var recipes = await _recipeRepository.GetAllAsync();
                    if (!string.IsNullOrWhiteSpace(mealType) && MealTypes.IsValid(mealType))
                        recipes = recipes.Where(r => r.MealType == mealType).ToList();
                    if (!string.IsNullOrWhiteSpace(prepTime) && PrepTimes.IsValid(prepTime))
                        recipes = recipes.Where(r => r.PrepTime == prepTime).ToList();

                    var summary = recipes.Select(r => new
                    {
                        r.Title, r.MealType, r.PrepTime,
                        Ingredients = r.GetIngredients().Select(i => i.Name).ToList(),
                    }).ToList();

                    var activity = $"Listed {recipes.Count} recipe(s)";
                    if (!string.IsNullOrWhiteSpace(mealType)) activity += $" (meal={mealType})";
                    if (!string.IsNullOrWhiteSpace(prepTime)) activity += $" (prep={prepTime})";
                    return (JsonSerializer.Serialize(summary), activity + ".");
                }
                case "evaluate_recipe_ingredients":
                {
                    var ingredients = new List<string>();
                    if (args.TryGetProperty("ingredients", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in arr.EnumerateArray())
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) ingredients.Add(s);
                        }
                    }

                    var pantryItems = await _pantryRepository.GetAllAsync();
                    var diff = IngredientMatcher.Diff(ingredients, pantryItems);
                    return (
                        JsonSerializer.Serialize(new { have = diff.Have, missing = diff.Missing }),
                        $"Checked {ingredients.Count} ingredient(s) against pantry: {diff.Have.Count} on hand, {diff.Missing.Count} missing."
                    );
                }
                default:
                    return ($"{{\"error\":\"Unknown tool '{name}'.\"}}", $"Model asked for unknown tool '{name}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool '{Tool}' invocation failed.", name);
            return ($"{{\"error\":\"{ex.Message}\"}}", $"Tool '{name}' errored: {ex.Message}");
        }
    }

    /// <summary>
    /// Tool definitions advertised to the model. These mirror the MCP tools by design —
    /// both surfaces expose the same capabilities with the same JSON Schemas.
    /// </summary>
    private static readonly ChatTool[] ChatTools =
    [
        ChatTool.CreateFunctionTool(
            functionName: "list_pantry",
            functionDescription: "Returns every item currently in the family's pantry and fridge. Use this whenever you need to know what ingredients are on hand.",
            functionParameters: BinaryData.FromString("""
                {"type":"object","properties":{},"required":[]}
            """)),

        ChatTool.CreateFunctionTool(
            functionName: "add_pantry_item",
            functionDescription: "Adds a single ingredient to the family's pantry or fridge.",
            functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "name": {"type": "string", "description": "The ingredient's common name (e.g. 'milk', 'cheddar cheese', 'eggs'). Lowercase, singular preferred."},
                        "category": {"type": "string", "enum": ["fridge", "pantry"], "description": "Where it's stored. Defaults to 'pantry' if omitted."},
                        "quantity": {"type": "string", "description": "Optional free-text quantity, e.g. '1 dozen', 'half gallon', '2 cans'."}
                    },
                    "required": ["name"]
                }
            """)),

        ChatTool.CreateFunctionTool(
            functionName: "list_recipes",
            functionDescription: "Returns the family's saved recipes, optionally filtered by meal type and prep time.",
            functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "meal_type": {"type": "string", "enum": ["breakfast", "lunch", "dinner", "any"], "description": "Optional filter."},
                        "prep_time": {"type": "string", "enum": ["quick", "standard"], "description": "Optional filter. 'quick' means 30 minutes or less."}
                    },
                    "required": []
                }
            """)),

        ChatTool.CreateFunctionTool(
            functionName: "evaluate_recipe_ingredients",
            functionDescription: "Given a list of ingredient names, returns which are on hand and which need to be bought. Use this to build a shopping list for a specific recipe or dish.",
            functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "ingredients": {
                            "type": "array",
                            "items": {"type": "string"},
                            "description": "Ingredient names to check, e.g. ['tortillas', 'ground beef', 'cheese', 'lettuce', 'salsa']."
                        }
                    },
                    "required": ["ingredients"]
                }
            """)),
    ];
}
