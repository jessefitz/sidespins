module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    let responseMessage = {
        "message": "Hello from Node."
    }

    const openAiApiKey = process.env.OPENAI_API_KEY;

    const requestOptions = {
        method: 'POST',
        url: 'https://api.openai.com/v1/chat/completions',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${openAiApiKey}`
        },
        data: {
            model: 'gpt-3.5-turbo',
            messages: [
                {role: "system", content: "You are a noob programmer that's super excited about your new AI creation."},
                {role: "user", content: "Give me a snarky one-liner to display on my website landing page."}
            ],
            temperature: 1
        }
    };

    try {
        const axios = require('axios');
        const response = await axios(requestOptions);
        if ((response.data.choices != null) && (response.data.choices.length > 0)) {
            responseMessage.message = response.data.choices[0].message.content
        }
    } catch (error) {
        responseMessage.message = "Error: " + error.message;
    }

    context.res = {
        body: JSON.stringify(responseMessage)
    };
}
