using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PICSeL.Configs;
using PICSeL.Models;
using System.Text;

namespace PICSeL.Utils
{
    public class DallEHelper
    {
        private DallEConfig _openAIConfig;
        private ILogger<AzureAIHelper> _logger;

        public DallEHelper(IOptions<DallEConfig> config, ILogger<AzureAIHelper> logger)
        {
            _openAIConfig = config.Value;
            _logger = logger;
        }

        public Dictionary<string, string> GetImages(IList<string> message)
        {
            Dictionary<string, string> images = new Dictionary<string, string>();
            int count = 0;
            foreach (var item in message)
            {
                var response = GetResponseFromOpenAI(item, GetImagePrompt)?.Data?.FirstOrDefault();

                if(response?.Url == null)
                {
                    _logger.LogError($"Failed to get image for {item}, will retry");
                    Thread.Sleep(10000);
                    response = GetResponseFromOpenAI(item, GetImagePrompt)?.Data?.FirstOrDefault();
                }
                
                images.Add(Path.Combine(Directory.GetCurrentDirectory(), $"{++count}.jpeg"), response?.Url ?? string.Empty);
            }

            return images;
        }

        private DallEResponse? GetResponseFromOpenAI(string content, Func<string, string> getPrompt)
        {
            var client = new HttpClient();

            client.DefaultRequestHeaders.Add("api-key", _openAIConfig.ApiKey);

            var request = new HttpRequestMessage(HttpMethod.Post, _openAIConfig.Uri)
            {
                Content = new StringContent(getPrompt(content), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Connection", "keep-alive");

            // Send the request and get the response
            var response = client.SendAsync(request).Result;

            if (response.IsSuccessStatusCode)
            {
                // Read the response content as a string
                string jsonResponse = response.Content.ReadAsStringAsync().Result;

                // Deserialize the JSON response into an OpenAiResponse object
                DallEResponse openAiResponse = JsonConvert.DeserializeObject<DallEResponse>(jsonResponse);

                openAiResponse.Status = response.StatusCode;

                return openAiResponse;
            }

            return new DallEResponse()
            {
                Status = response.StatusCode,
            };

        }

        private string GetImagePrompt(string message)
        {
            return JsonConvert.SerializeObject(new
            {
                prompt = message,
                quality = "standard",
                size = "1024x1024",
                style = "vivid"
            });
        }
    }
}
