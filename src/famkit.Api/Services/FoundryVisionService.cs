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
    private const string PromptFileName = "vision-system-prompt.txt";

    private readonly ChatClient? _chatClient;
    private readonly ILogger<FoundryVisionService> _logger;
    private readonly string? _systemPrompt;

    public FoundryVisionService(IConfiguration configuration, ILogger<FoundryVisionService> logger)
    {
        _logger = logger;

        var promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", PromptFileName);
        try
        {
            _systemPrompt = File.ReadAllText(promptPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not read vision system prompt from {PromptPath}; vision identification will fail until it's available.", promptPath);
        }

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

        if (_systemPrompt is null)
        {
            throw new InvalidOperationException($"Vision system prompt could not be loaded from Prompts/{PromptFileName}.");
        }

        List<ChatMessage> messages =
        [
            new SystemChatMessage(_systemPrompt),
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
