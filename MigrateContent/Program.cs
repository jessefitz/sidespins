using Newtonsoft.Json.Linq;
using System.IO;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace MigrateContent
{
    //call with first parameter = true to initiate a content migration to prod in addition to the migration to dev.
    class Program
    {
         static async Task Main(string[] args)
        {
            bool migrateToProd = false; // default value

            // Check if the "migrateToProd" parameter was passed as an argument
            if (args.Length > 0 && bool.TryParse(args[0], out bool argValue))
            {
                migrateToProd = argValue;
            }

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
            string containerName = (string)localSettings["Values"]["CosmosContainerName"];
            string keyString = (string)localSettings["Values"]["CosmosKey"];
            string endPoint = (string)localSettings["Values"]["CosmosEndPoint"];
            string prodDBName = databaseName.Split('-')[0];
            string prodContainerName = containerName.Split('-')[0];

            
            // Create a Cosmos DB client instance
            CosmosClient client = new CosmosClient(endPoint, keyString);

            // Get a reference to the database and container
            Database devDatabase = await client.GetDatabase(databaseName).ReadAsync();
            Container devContainer = await devDatabase.GetContainer(containerName).ReadContainerAsync();
            // Delete the container
            await devContainer.DeleteContainerAsync();
            // Recreate the container
            await devDatabase.CreateContainerIfNotExistsAsync(containerName, "/id");
            devContainer = await devDatabase.GetContainer(containerName).ReadContainerAsync();

            Database prodDB  = await client.GetDatabase(prodDBName).ReadAsync();
            Container prodContainer = await prodDB.GetContainer(prodContainerName).ReadContainerAsync();

            if(migrateToProd){
                // Delete the prod container
                await prodContainer.DeleteContainerAsync();
                // Recreate the prod container
                await prodDB.CreateContainerIfNotExistsAsync(prodContainerName, "/id");
                prodContainer = await prodDB.GetContainer(prodContainerName).ReadContainerAsync();
            }


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
                    dynamic response = await devContainer.CreateItemAsync(new { 
                            id = id, 
                            content = content,
                            urlpath = urlPath,
                            title = title,
                            tagline  = tagline,
                            date = date,
                            rank = rank,
                            category = category });
                            
                    if(migrateToProd){
                        dynamic responseFromProd = await prodContainer.CreateItemAsync(new { 
                            id = id, 
                            content = content,
                            urlpath = urlPath,
                            title = title,
                            tagline  = tagline,
                            date = date,
                            rank = rank,
                            category = category });
                    }

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

