namespace PICSeL.Utils
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Microsoft.Extensions.Options;
    using Newtonsoft.Json;
    using PICSeL.Configs;
    using PICSeL.Models;

    public class AzureSpeechHelper
    {
        private readonly AzureAvatarConfig _avatarConfig;
        private readonly string _endpoint;
        private readonly ILogger<AzureSpeechHelper> _logger;

        public AzureSpeechHelper(IOptions<AzureAvatarConfig> avatarConfig, ILogger<AzureSpeechHelper> logger)
        {
            _avatarConfig = avatarConfig.Value;
            _logger = logger;
            _endpoint = $"https://{avatarConfig.Value.Region}.{avatarConfig.Value.Servicehost}/api/texttospeech/3.1-preview1/batchsynthesis/talkingavatar";
        }
        public async Task<string> SubmitSynthesisAsync(string ssml)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _avatarConfig.SubscriptionKey);

                var payload = new
                {
                    displayName = "PICSeL",
                    description = "This is going to be a video for PICSeL", 
                    textType = "SSML",
                    inputs = new[]
                    {
                        new
                        {
                            text = ssml 
                        }
                    },
                    properties = new
                    {
                        talkingAvatarCharacter = "lisa",
                        talkingAvatarStyle = "casual-sitting",
                        videoFormat = "mp4",
                        videoCodec = "h264",
                        subtitleType = "soft_embedded",
                        backgroundColor = "white",
                    },
                    customVoices = new { }
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.Indented); // Use Indented if you want to see the JSON formatted nicely in debug, else remove it
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(_endpoint, content);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var jobResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    _logger.LogTrace("Batch avatar synthesis job submitted successfully");
                    return jobResponse.id;
                }
                else
                {
                    _logger.LogError($"Failed to submit batch avatar synthesis job: {await response.Content.ReadAsStringAsync()}");
                    throw new HttpRequestException("Failed to submit batch avatar synthesis job", new ApplicationException(), System.Net.HttpStatusCode.InternalServerError);
                }
            }
        }

        public async Task<VideoContentResponse> GetSynthesisAsync(string jobId)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _avatarConfig.SubscriptionKey);

                var response = await client.GetAsync($"{_endpoint}/{jobId}");

                var responseContent = await response.Content.ReadAsStringAsync();
                var jobResponse = JsonConvert.DeserializeObject<VideoContentResponse>(responseContent);
                _logger.LogTrace($"Batch synthesis job status: {jobResponse.Status}");
                return jobResponse;
            }
        }
    }

}
