class OpenAiHelper {
    constructor() {
      // You can initialize any properties you need here
    }
  
    async callGpt3Turbo(role, prompt, temp){
        let responseMessage = "";
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
                    {role: "system", content: role},
                    {role: "user", content: prompt}
                ],
                temperature: temp
            }
        };

        try {
            const axios = require('axios');
            const response = await axios(requestOptions);
            if ((response.data.choices != null) && (response.data.choices.length > 0)) {
                responseMessage = response.data.choices[0].message.content
            }
        } catch (error) {
            responseMessage = "Error: " + error.message;
        }

        return responseMessage;
        // context.res = {
        //     body: JSON.stringify(responseMessage)
        // };
    }
}
  
  module.exports = OpenAiHelper;
  