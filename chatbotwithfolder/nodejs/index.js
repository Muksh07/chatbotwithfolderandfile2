const express = require('express');
const { GoogleGenerativeAI } = require('@google/generative-ai');
require('dotenv').config();

const app = express();
const port = 5000;

// Middleware to parse JSON requests
app.use(express.json());

// Ensure the API key is correctly loaded from the environment variable
const apiKey = process.env.API_KEY;

const genAI = new GoogleGenerativeAI(apiKey);
const model = genAI.getGenerativeModel({ model: 'gemini-1.5-flash' });

app.post('/generate', async (req, res) => {
  const { prompt } = req.body;
  try {
    const result = await model.generateContent(prompt);

    // Log the entire result object in a more readable format
    const resultString = JSON.stringify(result, (key, value) => {
      if (typeof value === 'function') {
        return '[Function]';
      }
      return value;
    }, 2);
    console.log('Raw result:', resultString);

    let generatedText = "No meaningful response generated.";
    
    if (result && result.response) {
      const response = result.response;

      if (response.candidates && response.candidates.length > 0) {
        const candidate = response.candidates[0];

        if (candidate.content && candidate.content.parts) {
          const parts = candidate.content.parts;
          generatedText = parts.map(part => part.text).join('').trim();
        } else if (candidate.content && candidate.content.text) {
          generatedText = candidate.content.text.trim();
        } else if (candidate.finishReason) {
          generatedText = `Response finished with reason: ${candidate.finishReason}`;
        } else {
          generatedText = "Candidate has no content or text.";
        }
      } else {
        generatedText = "No candidates found in the response.";
      }
    } else {
      generatedText = "No valid response structure found.";
    }
    
    res.send(generatedText);

  } catch (error) {
    console.error('Error generating content:', error);
    res.status(500).json({ error: 'Error generating content', details: error.message });
  }
});

app.listen(port, () => {
  console.log(`Service running at http://localhost:${port}`);
});
