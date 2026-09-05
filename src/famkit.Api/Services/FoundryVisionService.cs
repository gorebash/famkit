using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using famkit.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace famkit.Services;

public class FoundryVisionService
{
    private const string SystemPrompt =
        "You are a kitchen inventory assistant. Look at the photo of a fridge or pantry and identify every " +
        "distinct food item you can see. Respond with ONLY a JSON array (no markdown, no commentary) where each " +
        "element has the shape: {\"name\": string, \"estimatedQuantity\": string, \"category\": \"fridge\"|\"pantry\"}. " +
        "Use short, common ingredient names (e.g. \"milk\", \"cheddar cheese\", \"eggs\").\n\n" +
        "You must always give your best estimate for estimatedQuantity — never leave it blank or null. Look at " +
        "the physical evidence in the photo and reason about it the same way a person glancing in the fridge would:\n" +
        "- Countable items: count them (\"3\", \"6 eggs\", \"2 bell peppers\").\n" +
        "- Packaged items where you can't see inside: guess from the package's typical size (\"1 gallon\", \"1 dozen\", \"1 lb block\", \"12 oz bottle\").\n" +
        "- Open containers or produce in bulk: judge how full or how much is left (\"about half full\", \"nearly empty\", \"a few handfuls\").\n" +
        "A rough, clearly-labeled guess (e.g. \"looks about half full\") is far more useful than no answer at all. " +
        "Only fall back to \"unknown\" in the rare case where the item is almost entirely hidden or obscured.\n\n" +
        "Example response:\n" +
        "[{\"name\": \"milk\", \"estimatedQuantity\": \"half gallon, about 3/4 full\", \"category\": \"fridge\"}, " +
        "{\"name\": \"eggs\", \"estimatedQuantity\": \"6 remaining\", \"category\": \"fridge\"}, " +
        "{\"name\": \"ketchup\", \"estimatedQuantity\": \"1 bottle, nearly full\", \"category\": \"pantry\"}]";

    private readonly ChatClient? _chatClient;
    private readonly ILogger<FoundryVisionService> _logger;

    public FoundryVisionService(IConfiguration configuration, ILogger<FoundryVisionService> logger)
    {
        _logger = logger;

        var endpoint = configuration["Foundry:Endpoint"];
        var apiKey = configuration["Foundry:ApiKey"];
        var deploymentName = configuration["Foundry:DeploymentName"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _logger.LogWarning("Foundry configuration is incomplete; vision identification will fail until Foundry:Endpoint, Foundry:ApiKey and Foundry:DeploymentName are set.");
            return;
        }

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = azureClient.GetChatClient(deploymentName);
    }

    public async Task<IdentifyResponse> IdentifyIngredientsAsync(BinaryData imageBytes, string mediaType, CancellationToken cancellationToken = default)
    {
        if (_chatClient is null)
        {
            throw new InvalidOperationException("Foundry vision client is not configured. Set Foundry:Endpoint, Foundry:ApiKey and Foundry:DeploymentName in local.settings.json.");
        }

        List<ChatMessage> messages =
        [
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart("Identify the food items in this photo."),
                ChatMessageContentPart.CreateImagePart(imageBytes, mediaType)
            ),
        ];

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        var rawText = completion.Content.Count > 0 ? completion.Content[0].Text : "[]";

        var ingredients = ParseIngredients(rawText);
        return new IdentifyResponse(ingredients);
    }

    private List<IdentifiedIngredient> ParseIngredients(string rawText)
    {
        var json = ExtractJsonArray(rawText);

        try
        {
            var ingredients = JsonSerializer.Deserialize<List<IdentifiedIngredient>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            return ingredients ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse ingredient JSON from Foundry response: {RawText}", rawText);
            return [];
        }
    }

    private static string ExtractJsonArray(string text)
    {
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        return start >= 0 && end > start ? text[start..(end + 1)] : "[]";
    }
}
