using System.Text.Json;
using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using Azure.Identity;
using famkit.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;

// OpenAI.Responses / Azure.AI.Extensions.OpenAI Responses bridge is marked experimental (OPENAI001).
#pragma warning disable OPENAI001

namespace famkit.Services;

public record ChatRequestDto(string Message, string? PreviousResponseId);
public record ChatResponseDto(string Reply, List<string> ToolActivity, string ResponseId);

/// <summary>
/// Handles conversational chat by calling a Foundry Prompt Agent (Foundry:ChatAgentName) over the
/// Responses API, rather than calling a model directly. The agent owns its own system prompt and
/// tool schemas (configured in Foundry, not in this code) — this service's job is just to run the
/// tool-calling round trip: send the conversation, execute whatever tool the agent asks for against
/// our repositories, feed the result back, repeat until the agent produces a final reply.
///
/// Conversation state lives server-side in Foundry, chained via previousResponseId (both across
/// separate chat turns AND across tool round trips within one turn) rather than us resending the
/// full message/tool-call history on every call. The caller only ever sends the newest message plus
/// the response id it got back last time — that's also what makes a repeated tool call within the
/// same conversation actually get skipped (the model can see it already has that data via the chain),
/// instead of re-invoking it on every new message because it never saw its own past tool results.
///
/// Auth is Entra ID (DefaultAzureCredential) rather than the API key used elsewhere in this app —
/// Foundry's Agent Service requires it. Locally this resolves via the developer's `az login` session.
/// </summary>
public class ChatService
{
    private const int MaxToolIterations = 5;

    private readonly ProjectResponsesClient? _responsesClient;
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

        var projectEndpoint = configuration["Foundry:ProjectEndpoint"];
        var agentName = configuration["Foundry:ChatAgentName"];

        if (string.IsNullOrWhiteSpace(projectEndpoint) || string.IsNullOrWhiteSpace(agentName))
        {
            _logger.LogWarning("Foundry chat agent configuration is incomplete; chat will fail until Foundry:ProjectEndpoint and Foundry:ChatAgentName are set.");
            return;
        }

        var projectClient = new AIProjectClient(new Uri(projectEndpoint), new DefaultAzureCredential());
        _responsesClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(defaultAgent: agentName);
    }

    public async Task<ChatResponseDto> ChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default)
    {
        if (_responsesClient is null)
        {
            throw new InvalidOperationException("Foundry chat agent is not configured. Set Foundry:ProjectEndpoint and Foundry:ChatAgentName in local.settings.json.");
        }

        List<ResponseItem> input = [ResponseItem.CreateUserMessageItem(request.Message)];
        string? previousResponseId = request.PreviousResponseId;

        var toolActivity = new List<string>();

        for (int iter = 0; iter < MaxToolIterations; iter++)
        {
            var result = await _responsesClient.CreateResponseAsync(input, previousResponseId, cancellationToken);
            var response = result.Value;

            var functionCalls = response.OutputItems.OfType<FunctionCallResponseItem>().ToList();
            if (functionCalls.Count == 0)
            {
                var reply = string.Concat(
                    response.OutputItems.OfType<MessageResponseItem>()
                        .SelectMany(m => m.Content)
                        .Select(c => c.Text));
                return new ChatResponseDto(reply, toolActivity, response.Id);
            }

            // Chain off this response and send only the new tool outputs — Foundry already has the
            // user message and the function-call items server-side as part of this response.
            previousResponseId = response.Id;
            input = [];
            foreach (var call in functionCalls)
            {
                var (toolResult, activityLine) = await InvokeToolAsync(call.FunctionName, call.FunctionArguments.ToString());
                toolActivity.Add(activityLine);
                input.Add(ResponseItem.CreateFunctionCallOutputItem(call.CallId, toolResult));
            }
        }

        _logger.LogWarning("Chat loop hit MaxToolIterations={Max} without a final response.", MaxToolIterations);
        return new ChatResponseDto(
            "I got tangled up trying to look that up. Could you rephrase?",
            toolActivity,
            previousResponseId ?? "");
    }

    private async Task<(string result, string activity)> InvokeToolAsync(string name, string argsJson)
    {
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
}
