using famkit.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace famkit.Functions;

public class VisionFunctions(FoundryVisionService foundryVisionService, ILogger<VisionFunctions> logger)
{
    [Function("IdentifyIngredients")]
    public async Task<IActionResult> IdentifyIngredients(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "vision/identify")] HttpRequest req)
    {
        var mediaType = string.IsNullOrWhiteSpace(req.ContentType) ? "image/jpeg" : req.ContentType;
        var imageBytes = await BinaryData.FromStreamAsync(req.Body);

        if (imageBytes.ToMemory().Length == 0)
        {
            return new BadRequestObjectResult("Upload a photo as the raw request body with an image Content-Type.");
        }

        try
        {
            var result = await foundryVisionService.IdentifyIngredientsAsync(imageBytes, mediaType, req.HttpContext.RequestAborted);
            return new OkObjectResult(result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Foundry vision is not configured.");
            return new ObjectResult(ex.Message) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
    }
}
