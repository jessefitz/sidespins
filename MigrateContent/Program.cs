using Newtonsoft.Json.Linq;
using System.IO;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace MigrateContent
{
    class Program
    {
         static async Task Main(string[] args)
        {
            // See https://aka.ms/new-console-template for more information
            Console.WriteLine("Hello, World!");

            string localSettingsPath = @"C:\projects\jessfitz.me\jessefitz_app\api\local.settings.json";
            string localSettingsContent = File.ReadAllText(localSettingsPath);
            JObject localSettings = JObject.Parse(localSettingsContent);
            string cosmosDbAccountName = (string)localSettings["Values"]["CosmosAccountName"];
            string resourceGroupName = (string)localSettings["Values"]["ResourceGroupName"];
            string databaseName = (string)localSettings["Values"]["CosmosDBName"];
            string containerName = (string)localSettings["Values"]["ComsosContainerName"];
            string keyString = (string)localSettings["Values"]["CosmosKey"];
            string endPoint = (string)localSettings["Values"]["CosmosEndPoint"];

            // Set the file path and read its contents
            string filePath = "C:\\projects\\jessfitz.me\\jessefitz_app\\src\\assets\\article-content\\bridge.md";
            string content = File.ReadAllText(filePath);

            // Create a new document to insert into Cosmos DB
            dynamic document = new
            {
                id = Guid.NewGuid().ToString(),
                content = content
            };

            // Create a Cosmos DB client instance
            CosmosClient client = new CosmosClient(endPoint, keyString);

            // Get a reference to the database and container
            Database database = await client.GetDatabase(databaseName).ReadAsync();
            Container container = await database.GetContainer(containerName).ReadContainerAsync();

            // Insert the document into Cosmos DB
            dynamic response = await container.CreateItemAsync(new { id = Guid.NewGuid().ToString(), content = content });

            // Output the response
            Console.WriteLine($"Inserted document with ID: {response.Resource.id}");
        }
    }
}

