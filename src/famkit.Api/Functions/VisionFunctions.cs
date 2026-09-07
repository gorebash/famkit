using famkit.Models;
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

    /// <summary>Accepts multipart/form-data with N image files, runs each through image-processor in parallel, then makes a second Foundry call to reconcile them into one deduplicated inventory list.</summary>
    [Function("IdentifyIngredientsBatch")]
    public async Task<IActionResult> IdentifyIngredientsBatch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "vision/identify-batch")] HttpRequest req)
    {
        if (!req.HasFormContentType)
        {
            return new BadRequestObjectResult("Send multipart/form-data with one or more image files.");
        }

        var form = await req.ReadFormAsync(req.HttpContext.RequestAborted);
        var files = form.Files;

        if (files.Count == 0)
        {
            return new BadRequestObjectResult("At least one image file is required.");
        }

        try
        {
            // Fan out: run every photo through image-processor in parallel.
            var perPhotoTasks = files.Select(async file =>
            {
                using var stream = file.OpenReadStream();
                var bytes = await BinaryData.FromStreamAsync(stream, req.HttpContext.RequestAborted);
                var mediaType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;
                return await foundryVisionService.IdentifyIngredientsAsync(bytes, mediaType, req.HttpContext.RequestAborted);
            });

            var perPhotoResults = await Task.WhenAll(perPhotoTasks);

            // Fan in: second Foundry call reconciles the N lists into one.
            var synthesized = await foundryVisionService.SynthesizeIngredientListAsync(perPhotoResults, req.HttpContext.RequestAborted);

            return new OkObjectResult(new BatchIdentifyResponse(
                Merged: synthesized,
                PerPhoto: perPhotoResults.ToList()
            ));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Foundry vision is not configured.");
            return new ObjectResult(ex.Message) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
    }
}

public record BatchIdentifyResponse(IdentifyResponse Merged, List<IdentifyResponse> PerPhoto);
