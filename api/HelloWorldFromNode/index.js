module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    // const name = (req.query.name || (req.body && req.body.name));
    // const responseMessage = name
    //     ? "Hello, " + name + ". This HTTP triggered function executed successfully."
    //     : "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.";

    const responseMessage = {
        "message": "Hello from Node."
    }

    // debugger; force a break here
    
    context.res = {
        // status: 200, /* Defaults to 200 */
        body: JSON.stringify(responseMessage) //to do:  this needs to be a json object in order to be correctly interprted by caller (at least as it's currently implemented in angular)
    };
}