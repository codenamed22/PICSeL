using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PICSeL.Models;
using PICSeL.Utils;
using System;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;

namespace PICSeL.Controllers
{
    [ApiController]
    [Route("/api/picsel")]
    public class VideoCreatorController : ControllerBase
    {
        private readonly ILogger<VideoCreatorController> _logger;
        private readonly AzureAIHelper _azureAIHelper;
        private readonly AzureSpeechHelper _azureSpeechHelper;
        private readonly DallEHelper _dallEHelper;

        public VideoCreatorController(AzureAIHelper azureAIHelper, AzureSpeechHelper azureSpeechHelper, DallEHelper dallEHelper, ILogger<VideoCreatorController> logger)
        {
            _logger = logger;
            _azureAIHelper = azureAIHelper;
            _azureSpeechHelper = azureSpeechHelper;
            _dallEHelper = dallEHelper;
        }

        [HttpGet("GetVideoForTopic")]
        public async Task<VideoContentResponse> GetVideoForTopic([FromQuery] string topicName, bool isQuery=false) 
        {
            await createScriptAndVideoAsync(topicName);
            var ssml = _azureAIHelper.GetSSMLFromScript(script);
            return new VideoContentResponse();
                    if (jobResponse.Status == "Succeeded")
            //throw new HttpRequestException("Unexpected", new InvalidOperationException("Something went wrong please try again"), HttpStatusCode.ServiceUnavailable);
        }

        [HttpGet("GetAnswerForQuery")]
        public async Task<VideoContentResponse> GetAnswerForQuery([FromQuery] string query)
        {
            var script = _azureAIHelper.GetAnswerForQuery(query);
                    else
                    {
                        _logger.LogTrace($"Batch avatar synthesis job is still running, status");
                        await Task.Delay(5000); // Wait for 5 seconds before polling again
                    }
                }
            }

            throw new HttpRequestException("Unexpected", new InvalidOperationException("Something went wrong please try again"), HttpStatusCode.ServiceUnavailable);
        }

        [HttpGet("GetAnswerForQuery")]
        public async Task<VideoContentResponse> GetAnswerForQuery([FromQuery] string query)
        {
            var script = _azureAIHelper.GetAnswerForQuery(query);
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
            await createScriptAndVideoAsync(summaryOfText);

            return new VideoContentResponse();

            //throw new HttpRequestException("Unexpected", new InvalidOperationException("Something went wrong please try again"), HttpStatusCode.ServiceUnavailable);
        }

        private async Task createScriptAndVideoAsync(string finalContent)
        {
            var script = _azureAIHelper.GetVideoScript(finalContent);

            IList<string> visuals = ExtractVisuals(script);
            var images = _dallEHelper.GetImages(visuals);

            foreach (var item in images)
            {
                await DownloadImageFromSasUriAsync(item.Value, item.Key);
            }

            var ssml = _azureAIHelper.GetSSMLFromScript(script);
            ssml = ssml.Replace("```", "");
            if (ssml.StartsWith("xml"))
            {
                ssml = ssml.Substring(4);
            }

            int numOfImages = 0;
            foreach (var item in images)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    numOfImages++;
                }
            }

            var avatarResponse = GetAvatarVideoAsync(ssml).Result;

            int durationPerImage = (TimeSpan.FromTicks(avatarResponse.Properties.DurationInTicks).Seconds) / numOfImages;

            var avatarVideoName = $"{Guid.NewGuid()}.mp4";
            await DownloadImageFromSasUriAsync(avatarResponse.Outputs.Result, Path.Combine(Directory.GetCurrentDirectory(), avatarVideoName));
            CreateImageVideo(images, avatarVideoName, durationPerImage);
        }

        private async Task<VideoContentResponse> GetAvatarVideoAsync(string ssml)
        {
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

            return null;
        }

        private List<string> ExtractVisuals(string input)
        {
            var visuals = new List<string>();
            var matches = Regex.Matches(input, @"\[Cut to visual: (.*?)\]");

            foreach (Match match in matches)
            {
                visuals.Add(match.Groups[1].Value);
            }

            return visuals;
        }

        private async Task DownloadImageFromSasUriAsync(string sasUri, string savePath)
        {
            if(sasUri == "")
            {
                return;
            }
            var blobClient = new BlobClient(new Uri(sasUri));

            BlobDownloadInfo download = await blobClient.DownloadAsync();

            using (FileStream file = System.IO.File.OpenWrite(savePath))
            {
                await download.Content.CopyToAsync(file);
            }
        }

        private async Task CreateImageVideo(Dictionary<string,string> images, string avatarVideoName, int durationPerImage)
        {
            durationPerImage = durationPerImage == 0 ? 13 : durationPerImage;
            // After downloading images
            List<string> videoFiles = new List<string>();
            foreach (var item in images)
            {
                if (string.IsNullOrEmpty(item.Value))
                {
                    continue;
                }
                string imageFilePath = item.Key.Replace("\\", "/"); // The path where the image is saved
                string videoFilePath = Path.ChangeExtension(imageFilePath, ".mp4");
                videoFiles.Add(videoFilePath);

                ExecuteFfMpegCommand($" -loop 1 -i \"{imageFilePath}\" -c:v libx264 -t {durationPerImage} -pix_fmt yuv420p -vf \"scale=1920:1080\" \"{videoFilePath}\"");
                System.IO.File.Delete(imageFilePath);
            }

            // Create the file list for concatenation
            string fileListPath = $"{avatarVideoName}file_list.txt";
            using (StreamWriter file = new StreamWriter(fileListPath))
            {
                foreach (string videoFile in videoFiles)
                {
                    file.WriteLine($"file '{videoFile}'");
                }
            }

            ExecuteFfMpegCommand($"-f concat -safe 0 -i {fileListPath} -c copy outputTemp.mp4");

            var input1 = Path.Combine(Directory.GetCurrentDirectory(), $"{avatarVideoName}");
            var input2 = Path.Combine(Directory.GetCurrentDirectory(), $"outputTemp.mp4");
            input1 = input1.Replace("\\", "/");
            input2 = input2.Replace("\\", "/");
            ////ExecuteFfMpegCommand($"-i \"{input1}\" -i \"{input2}\" -filter_complex \"[1:v]scale=320:-1[ovrl]; [0:v][ovrl]overlay=W-w-10:10\" -codec:a copy output_pip.mp4"); PIP

            // For Side-by-Side
            ExecuteFfMpegCommand($"-i \"{input1}\" -i \"{input2}\" -filter_complex \"[0:v][1:v]scale2ref=h=ih/1:w=iw/1[main][pip];[main][pip]hstack\" -codec:a copy {avatarVideoName}_side_by_side.mp4");

            var output_video = Path.Combine(Directory.GetCurrentDirectory(), $"{avatarVideoName}_side_by_side.mp4");
            var output_video2 = Path.Combine(Directory.GetCurrentDirectory(), $"{avatarVideoName}_side_by_side_streamable.m3u8");
            ConvertMp4ToHls(output_video, output_video2);

            System.IO.File.Delete(input1);
            System.IO.File.Delete(input2);

        }

        private void ExecuteFfMpegCommand(string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg", // Ensure ffmpeg is in PATH or specify its full path
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                try
                {
                    process.Start();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(stderr))
                    {
                        _logger.LogWarning(stderr);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"Error executing FFmpeg command: {e.Message}");
                }
            }
        }

        private void ConvertMp4ToHls(string inputFilePath, string outputDirectory)
        {
            ExecuteFfMpegCommand($"-i \"{inputFilePath}\" -codec: copy -start_number 0 -hls_time 10 -hls_list_size 0 -f hls \"{outputDirectory}\"");
        }


    }
}
