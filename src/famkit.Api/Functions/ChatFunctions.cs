using famkit.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace famkit.Functions;

public class ChatFunctions(ChatService chatService, ILogger<ChatFunctions> logger)
{
    [Function("Chat")]
    public async Task<IActionResult> Chat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chat")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<ChatRequestDto>();
        if (request is null || request.Messages is null || request.Messages.Count == 0)
        {
            return new BadRequestObjectResult("At least one message is required.");
        }

        try
        {
            var response = await chatService.ChatAsync(request, req.HttpContext.RequestAborted);
            return new OkObjectResult(response);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Chat service not configured.");
            return new ObjectResult(ex.Message) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
    }
}
