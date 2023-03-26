class CosmosArticleHelper {

    constructor() {
    }

    async getArticleContent(articlePath){
        const { CosmosClient } = require('@azure/cosmos');
        const endpoint = process.env.CosmosEndPoint;
        const key = process.env.CosmosKey;
        const client = 
            new CosmosClient({ endpoint, key });
        const databaseId = 'JesseFitzAppDB-Dev'; //TODO:  pull from settings
        const containerId = 'JesseFitzAppContainer-Dev'; //TODO:  pull from settings
        
        try {
            
            const { database } = await client.databases.createIfNotExists({ id: databaseId });
            const { container } = await database.containers.createIfNotExists({ id: containerId });
            
            const querySpec = {
                query: `SELECT * FROM c WHERE c.urlpath = "${articlePath}"`
            };
            
            const { resources } = await container.items.query(querySpec).fetchAll();
            console.log(resources);

            if( resources != null && resources.length > 0)
            {            
                return resources[0];
            }
            else
            {
                return null;
            }
        }
        catch(error){
            return "Error in getArticleContent Helper class.";
        }
    }
}


module.exports = CosmosArticleHelper;
  