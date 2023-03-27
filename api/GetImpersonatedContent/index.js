module.exports = async function (context, req) {
    // Import the helper class, which abstracts calls to the OpenAI API
    
    let responseMessage = "";
    try
    {
        let proceed = false;

        const OpenAiHelper = require('../OpenAiHelper');
        const ArticleHelper = require('../CosmosArticleHelper');

        const persona = req.query.persona || req.body.persona; //Ex. 'a struggling stand-up comedian'
        const articlePath = req.query.articlePath || req.body.articlePath;

        //MAKE THE CALL TO COSMOS
        const articleHelper = new ArticleHelper(); 
        const article = await articleHelper.getArticleContent(articlePath);

        if (article === null  || article === undefined || article.content === '') {
            // The variable is empty, null, or undefined
            responseMessage = 'Failed to retrieve article content.';
            proceed = false;
        } else {
            if (persona === null || persona === '' || persona === undefined) {
                // The variable is empty, null, or undefined
                responseMessage = 'The persona variable is empty, null, or undefined.';
                proceed = false;
            } else {
                // The variable is not empty, null, or undefined
                proceed = true;
            }
        }
        
        if(proceed){
            const openAiHelper = new OpenAiHelper();
            let decodedPersona = decodeURIComponent(persona);

            const role = 'You are ' + persona + ".";
            const prompt = 'Rephrase this markdown content according to your role as ' +
            decodedPersona + '.  Return only markdown content.  Do not alter the image elements.  The content to translate is: ' + article.content;
            const temp = 1; // Choose the desired temperature value

            try {
                const result = await openAiHelper.callGpt3Turbo(role, prompt, temp);
                // responseMessage = '{ content: "' + result + '"}';
                const objectResult = {content: result};
                responseMessage = JSON.stringify(objectResult);
                console.log('GPT-3 Turbo response:', result);
            } catch (error) {
                const objectResult = {content: error};            
                responseMessage = JSON.stringify(objectResult);
                console.error('Error calling GPT-3 Turbo:', error);
            }
        }
    }
    catch (error)
    {
        const objectResult = {content: error};            
        responseMessage = JSON.stringify(objectResult);
        console.error('Error in GetImpersonatedContent API.', error);
    }
      
    context.res = {
        // status: 200, /* Defaults to 200 */v   
        body: responseMessage
    };
}