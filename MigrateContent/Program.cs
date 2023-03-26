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
            //
            string articleDirectoryPath = @"C:\projects\jessfitz.me\jessefitz_app\src\assets\article-directory.json";
            string articleDirectoryContent = File.ReadAllText(articleDirectoryPath);       
            JArray articleDirectory = JArray.Parse(articleDirectoryContent);

            string localSettingsPath = @"C:\projects\jessfitz.me\jessefitz_app\api\local.settings.json";
            string localSettingsContent = File.ReadAllText(localSettingsPath);
            JObject localSettings = JObject.Parse(localSettingsContent);
            string cosmosDbAccountName = (string)localSettings["Values"]["CosmosAccountName"];
            string resourceGroupName = (string)localSettings["Values"]["ResourceGroupName"];
            string databaseName = (string)localSettings["Values"]["CosmosDBName"];
            string containerName = (string)localSettings["Values"]["ComsosContainerName"];
            string keyString = (string)localSettings["Values"]["CosmosKey"];
            string endPoint = (string)localSettings["Values"]["CosmosEndPoint"];

            
            // Create a Cosmos DB client instance
            CosmosClient client = new CosmosClient(endPoint, keyString);

            // Get a reference to the database and container
            Database database = await client.GetDatabase(databaseName).ReadAsync();
            Container container = await database.GetContainer(containerName).ReadContainerAsync();

            // Delete the container
            await container.DeleteContainerAsync();

            // Recreate the container
            await database.CreateContainerIfNotExistsAsync(containerName, "/id");
            container = await database.GetContainer(containerName).ReadContainerAsync();
            int countInserted = 0;
            int countFailed = 0;

            foreach (JObject article in articleDirectory)
            {
                try
                {
                    string id = (string)article["id"];
                    string urlPath = (string)article["urlpath"];
                    string title = (string)article["title"];
                    string src = (string)article["src"];
                    string tagline = (string)article["tagline"];
                    string date = (string)article["date"];
                    int rank = (int)article["rank"];
                    string category = (string)article["category"];
                    
                    // Do something with the article properties
                    Console.WriteLine($"Article {id}: {title} ({category})");
                    string filePath = "C:\\projects\\jessfitz.me\\jessefitz_app\\src\\assets\\article-content\\" + src;
                    string content = File.ReadAllText(filePath);

                    // Insert the document into Cosmos DB
                    dynamic response = await container.CreateItemAsync(new { id = id, content = content, urlpath = urlPath });

                    // Output the response
                    Console.WriteLine($"Inserted document with ID: {response.Resource.id}");
                    countInserted++;
                }
                catch(Exception e)
                {
                    Console.WriteLine($"Error inserting record for {(string)article["urlpath"]}: {e.ToString()}");
                    countFailed++;
                }
            }

            Console.WriteLine($"Successfully Inserted {countInserted} and failed to insert {countFailed}.");
            
            // Set the file path and read its contents
            // string filePath = "C:\\projects\\jessfitz.me\\jessefitz_app\\src\\assets\\article-content\\bridge.md";
        }
    }
}

