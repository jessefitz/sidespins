using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using SideSpins.Api.Services;
using System.Text.Json;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure JSON serialization options
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PropertyNameCaseInsensitive = true;
});

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

// Add CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalDevelopment", policy =>
    {
        policy.WithOrigins(
            "http://localhost:4000",  // Jekyll development server
            "http://127.0.0.1:4000",
            "https://localhost:4000",
            "https://127.0.0.1:4000"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// Register Cosmos DB
builder.Services.AddSingleton<CosmosClient>(serviceProvider =>
{
    var cosmosUri = Environment.GetEnvironmentVariable("COSMOS_URI");
    var cosmosKey = Environment.GetEnvironmentVariable("COSMOS_KEY");
    
    if (string.IsNullOrEmpty(cosmosUri) || string.IsNullOrEmpty(cosmosKey))
    {
        throw new InvalidOperationException("COSMOS_URI and COSMOS_KEY must be configured");
    }

    var cosmosClientOptions = new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    };

    return new CosmosClient(cosmosUri, cosmosKey, cosmosClientOptions);
});

builder.Services.AddScoped<CosmosService>(serviceProvider =>
{
    var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
    var databaseName = Environment.GetEnvironmentVariable("COSMOS_DB") ?? "sidespins";
    
    return new CosmosService(cosmosClient, databaseName);
});

// Build and run the application
builder.Build().Run();
