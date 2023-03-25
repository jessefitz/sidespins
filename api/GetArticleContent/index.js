module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger GetArticleContent function processed a request.');
  
    const { CosmosClient } = require('@azure/cosmos');
    const endpoint = process.env.CosmosEndPoint;
    const key = process.env.CosmosKey;
    const client = 
        new CosmosClient({ endpoint, key });
    const databaseId = 'JesseFitzAppDB-Dev';
    const containerId = 'JesseFitzAppContainer-Dev';
    
    try {
        
      const { database } = await client.databases.createIfNotExists({ id: databaseId });
      const { container } = await database.containers.createIfNotExists({ id: containerId });
      const articleId = req.query.ArticleId || req.body.ArticleId;
      
      const querySpec = {
        query: `SELECT * FROM c WHERE c.id = "${articleId}"`
      };
      
      const { resources } = await container.items.query(querySpec).fetchAll();
      console.log(resources);
      
      const valToReturn = JSON.stringify(resources);
      
      context.res = {
        status: 200, // set the appropriate status code
        body: valToReturn, // set the response body to the value to return
        headers: {
          "Content-Type": "application/json" // set the appropriate content type
        }
      };
      
    } catch (error) {
      console.error('Error connecting to Cosmos DB:', error);
      context.res = {
        status: 500,
        body: 'Error connecting to Cosmos DB'
      };
    }
  };
  