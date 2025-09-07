using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using SideSpins.Api.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SidesSpins.Functions;
var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure middleware
builder.Services.AddScoped<AuthenticationMiddleware>();
builder.UseMiddleware<AuthenticationMiddleware>();

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

builder.Services.AddScoped<LeagueService>(serviceProvider =>
{
    var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
    var databaseName = Environment.GetEnvironmentVariable("COSMOS_DB") ?? "sidespins";
    
    return new LeagueService(cosmosClient, databaseName);
});

// Register Player Service first (needed by MembershipService)
builder.Services.AddScoped<IPlayerService, CosmosPlayerService>(serviceProvider =>
{
    var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
    var logger = serviceProvider.GetRequiredService<ILogger<CosmosPlayerService>>();
    var databaseName = Environment.GetEnvironmentVariable("COSMOS_DB") ?? "sidespins";
    var containerName = Environment.GetEnvironmentVariable("COSMOS_PLAYERS_CONTAINER") ?? "Players";
    
    return new CosmosPlayerService(cosmosClient, logger, databaseName, containerName);
});

// Register Membership Service
builder.Services.AddScoped<IMembershipService, CosmosMembershipService>(serviceProvider =>
{
    var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
    var playerService = serviceProvider.GetRequiredService<IPlayerService>();
    var logger = serviceProvider.GetRequiredService<ILogger<CosmosMembershipService>>();
    var databaseName = Environment.GetEnvironmentVariable("COSMOS_DB") ?? "sidespins";
    var containerName = Environment.GetEnvironmentVariable("COSMOS_MEMBERSHIPS_CONTAINER") ?? "TeamMemberships";
    
    return new CosmosMembershipService(cosmosClient, playerService, logger, databaseName, containerName);
});

// Register HttpClient for Stytch API
builder.Services.AddHttpClient<AuthService>();

// Register Auth service
builder.Services.AddScoped<AuthService>(serviceProvider =>
{
    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    
    var projectId = Environment.GetEnvironmentVariable("STYTCH_PROJECT_ID");
    var secret = Environment.GetEnvironmentVariable("STYTCH_SECRET");
    var jwtSigningKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
    var logger = serviceProvider.GetRequiredService<ILogger<AuthService>>();
    
    if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(secret))
    {
        throw new InvalidOperationException("STYTCH_PROJECT_ID and STYTCH_SECRET must be configured");
    }
    
    if (string.IsNullOrEmpty(jwtSigningKey))
    {
        throw new InvalidOperationException("JWT_SIGNING_KEY must be configured");
    }

    return new AuthService(httpClient, projectId, secret, jwtSigningKey, logger);
});

// Build and run the application
builder.Build().Run();
