using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.Exporter;
using famkit.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["AzureWebJobsStorage"] ?? "UseDevelopmentStorage=true";
    return new TableServiceClient(connectionString);
});

builder.Services.AddSingleton<PantryRepository>();
builder.Services.AddSingleton<RecipeRepository>();
builder.Services.AddSingleton<FoundryVisionService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddHttpClient<SpoonacularClient>();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();
