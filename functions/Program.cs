using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using SideSpins.Api.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure JSON serialization options for Newtonsoft.Json
JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    ContractResolver = new CamelCasePropertyNamesContractResolver(),
    NullValueHandling = NullValueHandling.Ignore,
    DateTimeZoneHandling = DateTimeZoneHandling.Utc
};

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

// Only wire up CORS in code if an env flag is set (e.g., local dev)
var enableCodeCors = (Environment.GetEnvironmentVariable("ENABLE_CODE_CORS") ?? "false")
    .Equals("true", StringComparison.OrdinalIgnoreCase);

if (enableCodeCors)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ConfiguredOrigins", policy =>
        {
            var origins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            policy.WithOrigins(origins)
                  .WithMethods("GET","POST","PUT","PATCH","DELETE","OPTIONS")
                  .WithHeaders("authorization","content-type");
        });
    });
}

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
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.Default
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
