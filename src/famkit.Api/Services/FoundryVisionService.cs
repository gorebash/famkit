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
    private const string IdentifyPromptFile = "vision-system-prompt.txt";
    private const string SynthesisPromptFile = "vision-synthesis-prompt.txt";

    private readonly ChatClient? _chatClient;
    private readonly ILogger<FoundryVisionService> _logger;
    private readonly string? _identifyPrompt;
    private readonly string? _synthesisPrompt;

    public FoundryVisionService(IConfiguration configuration, ILogger<FoundryVisionService> logger)
    {
        _logger = logger;
        _identifyPrompt = TryReadPrompt(IdentifyPromptFile);
        _synthesisPrompt = TryReadPrompt(SynthesisPromptFile);

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
        var client = RequireClient();
        var prompt = RequirePrompt(_identifyPrompt, IdentifyPromptFile);

        List<ChatMessage> messages =
        [
            new SystemChatMessage(prompt),
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart("Identify the food items in this photo."),
                ChatMessageContentPart.CreateImagePart(imageBytes, mediaType)
            ),
        ];

        ChatCompletion completion = await client.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        var rawText = completion.Content.Count > 0 ? completion.Content[0].Text : "[]";
        return new IdentifyResponse(ParseIngredients(rawText));
    }

    /// <summary>Second-stage reconciliation: merges per-photo ingredient lists into one deduplicated inventory.</summary>
    public async Task<IdentifyResponse> SynthesizeIngredientListAsync(IReadOnlyList<IdentifyResponse> perPhotoResults, CancellationToken cancellationToken = default)
    {
        var client = RequireClient();
        var prompt = RequirePrompt(_synthesisPrompt, SynthesisPromptFile);

        // Fast path: a single photo doesn't need a second Foundry call.
        if (perPhotoResults.Count == 1)
        {
            return perPhotoResults[0];
        }

        var userText = BuildSynthesisUserMessage(perPhotoResults);

        List<ChatMessage> messages =
        [
            new SystemChatMessage(prompt),
            new UserChatMessage(userText),
        ];

        ChatCompletion completion = await client.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        var rawText = completion.Content.Count > 0 ? completion.Content[0].Text : "[]";
        return new IdentifyResponse(ParseIngredients(rawText));
    }

    private static string BuildSynthesisUserMessage(IReadOnlyList<IdentifyResponse> perPhotoResults)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Here are the ingredient lists identified from {perPhotoResults.Count} photos of the same fridge/pantry. Merge them into one deduplicated inventory.");
        sb.AppendLine();
        for (int i = 0; i < perPhotoResults.Count; i++)
        {
            sb.AppendLine($"Photo {i + 1}:");
            sb.AppendLine(JsonSerializer.Serialize(perPhotoResults[i].Ingredients));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private ChatClient RequireClient()
    {
        if (_chatClient is null)
        {
            throw new InvalidOperationException("Foundry vision client is not configured. Set Foundry:Endpoint, Foundry:ApiKey and Foundry:DeploymentName in local.settings.json.");
        }
        return _chatClient;
    }

    private static string RequirePrompt(string? prompt, string fileName)
    {
        if (prompt is null)
        {
            throw new InvalidOperationException($"Prompt file Prompts/{fileName} could not be loaded.");
        }
        return prompt;
    }

    private string? TryReadPrompt(string fileName)
    {
        var promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", fileName);
        try
        {
            return File.ReadAllText(promptPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not read prompt from {PromptPath}.", promptPath);
            return null;
        }
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
