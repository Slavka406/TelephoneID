using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TelephoneID.Services
{
  public class FraudDetectionService
  {
      private readonly HttpClient _httpClient;
      private readonly string _openAIApiKey;
      // Optionally: configuration for model or prompts could be included.

      public FraudDetectionService(IHttpClientFactory httpClientFactory, IConfiguration config)
      {
          _httpClient = httpClientFactory.CreateClient();
          _openAIApiKey = config["OpenAI:ApiKey"];  // OpenAI API Key from configuration
      }

      /// <summary>
      /// Analyzes the given transcript text using GPT-4 to determine if it's fraudulent.
      /// Returns true if fraud is likely, along with a reason.
      /// </summary>
      public bool AnalyzeTranscript(string transcript, out string reason)
      {
          reason = null;
          if (string.IsNullOrWhiteSpace(transcript))
              return false;

          // Construct a prompt for the model to identify fraud in the transcript.
          string prompt = 
              "You are a fraud detection AI. Analyze the following call transcript and respond with JSON {\"fraud\": true/false, \"reason\": \"...\"} indicating if the call is a scam:\n"
              + transcript;

          // Prepare OpenAI API request (using Chat Completions with GPT-4)
          var requestBody = new
          {
              model = "gpt-4",
              messages = new[] {
                  new { role = "user", content = prompt }
              },
              max_tokens = 50,
              temperature = 0.2
          };
          string requestJson = JsonSerializer.Serialize(requestBody);
          var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
          httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAIApiKey);
          httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

          HttpResponseMessage response;
          try 
          {
              response = _httpClient.Send(httpRequest);  // (In real code, use SendAsync and await it)
          }
          catch
          {
              return false; // API call failed (network error)
          }
          if (!response.IsSuccessStatusCode)
          {
              return false; // API returned an error (e.g., invalid key or model)
          }

          string responseJson = response.Content.ReadAsStringAsync().Result;
          // Parse the response to extract the assistant's message content
          using JsonDocument jsonDoc = JsonDocument.Parse(responseJson);
          JsonElement messageContent = jsonDoc.RootElement
                                            .GetProperty("choices")[0]
                                            .GetProperty("message")
                                            .GetProperty("content");
          string contentString = messageContent.GetString();
          // The content should be a JSON string like {"fraud": true, "reason": "phishing attempt"}.
          try
          {
              using JsonDocument resultDoc = JsonDocument.Parse(contentString);
              bool isFraud = resultDoc.RootElement.GetProperty("fraud").GetBoolean();
              if (isFraud)
              {
                  reason = resultDoc.RootElement.GetProperty("reason").GetString();
              }
              return isFraud;
          }
          catch
          {
              // If parsing fails (model didn't give expected JSON), we treat as no fraud detected.
              return false;
          }
      }
  }
}