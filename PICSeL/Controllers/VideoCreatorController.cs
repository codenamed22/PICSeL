using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Microsoft.AspNetCore.Mvc;
using PICSeL.Models;
using PICSeL.Utils;
using System;
using System.Net;

namespace PICSeL.Controllers
{
    [ApiController]
    [Route("/api/picsel")]
    public class VideoCreatorController : ControllerBase
    {
        private readonly ILogger<VideoCreatorController> _logger;
        private readonly AzureAIHelper _azureAIHelper;
        private readonly AzureSpeechHelper _azureSpeechHelper;

        public VideoCreatorController(AzureAIHelper azureAIHelper, AzureSpeechHelper azureSpeechHelper, ILogger<VideoCreatorController> logger)
        {
            _logger = logger;
            _azureAIHelper = azureAIHelper;
            _azureSpeechHelper = azureSpeechHelper;
        }

        [HttpGet("GetVideoForTopic")]
        public async Task<VideoContentResponse> GetVideoForTopic([FromQuery] string topicName)
        {
            var script = _azureAIHelper.GetVideoScript(topicName);
            var ssml = _azureAIHelper.GetSSMLFromScript(script);
            ssml = ssml.Replace("```", "");

            var jobId = await _azureSpeechHelper.SubmitSynthesisAsync(ssml);
            if (!string.IsNullOrEmpty(jobId))
            {
                while (true)
                {
                    var jobResponse = await _azureSpeechHelper.GetSynthesisAsync(jobId);
                    if (jobResponse.Status == "Succeeded")
                    {
                        return jobResponse;
                    }
                    if (jobResponse.Status == "Failed")
                    {
                        throw new HttpRequestException($"Batch avatar synthesis job failed");
                    }
                    else
                    {
                        _logger.LogTrace($"Batch avatar synthesis job is still running, status");
                        await Task.Delay(5000); // Wait for 5 seconds before polling again
                    }
                }
            }

            throw new HttpRequestException("Unexpected", new InvalidOperationException("Something went wrong please try again"), HttpStatusCode.ServiceUnavailable);
        }

        [HttpGet("GetVideoForContentFile")]
        public async Task<VideoContentResponse> GetVideoForContentFile([FromQuery] string fileSasUrl)
        {
            string fileContents = "";
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    try
                    {
                        fileContents = await httpClient.GetStringAsync(fileSasUrl);
                    }
                    catch (HttpRequestException e)
                    {
                        Console.WriteLine($"Error retrieving file: {e.Message}");
                    }
                }
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("File couldnt be read from the link, please check the link", e, HttpStatusCode.ServiceUnavailable);
            }

            var summaryOfText = _azureAIHelper.GetSumamryFromDocuments(fileContents);
            var script = _azureAIHelper.GetVideoScript(summaryOfText);
            var ssml = _azureAIHelper.GetSSMLFromScript(script);
            ssml = ssml.Replace("```", "");
            if(ssml.StartsWith("xml"))
            {
                ssml = ssml.Substring(4);
            }

            var jobId = await _azureSpeechHelper.SubmitSynthesisAsync(ssml);
            if (!string.IsNullOrEmpty(jobId))
            {
                while (true)
                {
                    var jobResponse = await _azureSpeechHelper.GetSynthesisAsync(jobId);
                    if (jobResponse.Status == "Succeeded")
                    {
                        return jobResponse;
                    }
                    if (jobResponse.Status == "Failed")
                    {
                        throw new HttpRequestException($"Batch avatar synthesis job failed");
                    }
                    else
                    {
                        _logger.LogTrace($"Batch avatar synthesis job is still running, status");
                        await Task.Delay(5000); // Wait for 5 seconds before polling again
                    }
                }
            }

            throw new HttpRequestException("Unexpected", new InvalidOperationException("Something went wrong please try again"), HttpStatusCode.ServiceUnavailable);
        }

        [HttpGet("GetVideoForQuery")]
        public async Task<VideoContentResponse> GetVideoForQuery([FromQuery] string query) 
        {
            var script = _azureAIHelper.GetQueryAnswer(query);

            var ssml = _azureAIHelper.GetSSMLFromScript(script);
            ssml = ssml.Replace("```", "");

            var jobId = await _azureSpeechHelper.SubmitSynthesisAsync(ssml);
            if (!string.IsNullOrEmpty(jobId))
            {
                while (true)
                {
                    var jobResponse = await _azureSpeechHelper.GetSynthesisAsync(jobId);
                    if (jobResponse.Status == "Succeeded")
                    {
                        return jobResponse;
                    }
                    if (jobResponse.Status == "Failed")
                    {
                        throw new HttpRequestException($"Batch avatar synthesis job failed");
                    }
                    else
                    {
                        _logger.LogTrace($"Batch avatar synthesis job is still running, status");
                        await Task.Delay(5000); // Wait for 5 seconds before polling again
                    }
                }
            }

            throw new HttpRequestException("Unexpected", new InvalidOperationException("Something went wrong please try again"), HttpStatusCode.ServiceUnavailable);
        }

        [HttpGet("GetTextAnswerForQuery")]
        public async Task<string> GetTextAnswerForQuery(string query, string targetLocale = "", string sourceLocale ="en-US")
        {
            try
            {
                var script = _azureAIHelper.GetQueryAnswer(query);
                //Convert it to desired locale
                if (!string.IsNullOrEmpty(targetLocale) && targetLocale != "en")
                    script = _azureAIHelper.GetLocalizedAnswerForQuery(script, targetLocale, sourceLocale);

                return script;
            }
            catch (Exception e)
            {
                throw new HttpRequestException("Unexpected", e, HttpStatusCode.ServiceUnavailable);
            }
            
            throw new HttpRequestException("Unexpected", new InvalidOperationException("Something went wrong please try again"), HttpStatusCode.ServiceUnavailable);
        }
    }
}
