using famkit.Models;
using famkit.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace famkit.Functions;

public class PantryFunctions(PantryRepository pantryRepository)
{
    [Function("GetPantry")]
    public async Task<IActionResult> GetPantry(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pantry")] HttpRequest req)
    {
        var items = await pantryRepository.GetAllAsync();
        return new OkObjectResult(items);
    }

    [Function("AddPantryItem")]
    public async Task<IActionResult> AddPantryItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pantry")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<PantryItemRequest>();
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return new BadRequestObjectResult("Name is required.");
        }

        var item = await pantryRepository.AddAsync(request);
        return new OkObjectResult(item);
    }

    [Function("DeletePantryItem")]
    public async Task<IActionResult> DeletePantryItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "pantry/{id}")] HttpRequest req,
        string id)
    {
        var deleted = await pantryRepository.DeleteAsync(id);
        return deleted ? new NoContentResult() : new NotFoundResult();
    }
}
