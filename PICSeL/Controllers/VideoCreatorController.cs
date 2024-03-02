using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PICSeL.Configs;
using PICSeL.Models;
using PICSeL.Utils;
using System;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
        private readonly BlobConfig _blobConfig;

        public VideoCreatorController(AzureAIHelper azureAIHelper, AzureSpeechHelper azureSpeechHelper, DallEHelper dallEHelper, ILogger<VideoCreatorController> logger, IOptions<BlobConfig> blobConfig)
        {
            _logger = logger;
            _azureAIHelper = azureAIHelper;
            _azureSpeechHelper = azureSpeechHelper;
            _dallEHelper = dallEHelper;
            _blobConfig = blobConfig.Value;
        }

        [HttpGet("GetVideoForTopic")]
        public string GetVideoForTopic([FromQuery] string topicName)
        {
            try
            {
                var jobId = Guid.NewGuid().ToString();
                Task.Run(() => createScriptAndVideoAsync(topicName, jobId));
                return jobId;
            }
            catch (Exception e)
            {
                throw new HttpRequestException("Unexpected", e, HttpStatusCode.ServiceUnavailable);
            }

            //throw new HttpRequestException("Unexpected", new InvalidOperationException("Something went wrong please try again"), HttpStatusCode.ServiceUnavailable);
        }

        [HttpGet("GetVideoForContentFile")]
        public async Task<string> GetVideoForContentFile([FromQuery] string fileSasUrl)
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

            try
            { 
                var summaryOfText = _azureAIHelper.GetSumamryFromDocuments(fileContents);

                var jobId = Guid.NewGuid().ToString();
                Task.Run(() => createScriptAndVideoAsync(summaryOfText, jobId));
                return jobId;
            }
            catch (Exception e)
            {
                throw new HttpRequestException("Unexpected", e, HttpStatusCode.ServiceUnavailable);
            }
        }

        private async Task<VideoContentResponse> createScriptAndVideoAsync(string finalContent, string projectGuid)
        {
            var script = _azureAIHelper.GetVideoScript(finalContent);

            _logger.LogInformation($"Fetched script for job: {projectGuid}");

            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), projectGuid));

            IList<string> visuals = ExtractVisuals(script);
            var images = _dallEHelper.GetImages(visuals, projectGuid);

            _logger.LogInformation($"Fetched images for job: {projectGuid}");

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

            try
            { 
                var x = XDocument.Parse(ssml);
            }
            catch (Exception e)
            {
                _logger.LogInformation($"SSML invalid for job: {projectGuid}");
                throw new HttpRequestException("SSML is not valid", e, HttpStatusCode.ServiceUnavailable);
            }

            int numOfImages = 0;
            foreach (var item in images)
            {
                if (!string.IsNullOrEmpty(item.Value))
                {
                    numOfImages++;
                }
            }

            try
            {
                var avatarResponse = await GetAvatarVideoAsync(ssml);

                _logger.LogInformation($"Fetched avatar video for: {projectGuid}");

                int durationPerImage = convertDuration(avatarResponse.Properties.Duration) / numOfImages;

                var avatarVideoName = $"{projectGuid}.mp4";
                await DownloadImageFromSasUriAsync(avatarResponse.Outputs.Result, Path.Combine(Directory.GetCurrentDirectory(), projectGuid, avatarVideoName));
                var sas = await CreateImageVideo(images, avatarVideoName, durationPerImage, projectGuid);

                avatarResponse.Outputs.Result = sas;

                Directory.Delete(Path.Combine(Directory.GetCurrentDirectory(), projectGuid), true);
                return avatarResponse;
            }
            catch (Exception e)
            {
                throw new HttpRequestException("Unexpected", e, HttpStatusCode.ServiceUnavailable);
            }
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

        private async Task<string> CreateImageVideo(Dictionary<string,string> images, string avatarVideoName, int durationPerImage, string projectGuid)
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
                System.IO.File.Delete(videoFilePath);
                videoFiles.Add(videoFilePath);

                ExecuteFfMpegCommand($" -loop 1 -i \"{imageFilePath}\" -c:v libx264 -t {durationPerImage} -pix_fmt yuv420p -vf \"scale=1920:1080\" \"{videoFilePath}\"");
                System.IO.File.Delete(imageFilePath);
            }

            _logger.LogInformation($"Created FFMPEG small videos for {projectGuid}");

            // Create the file list for concatenation
            string fileListPath = $"{avatarVideoName}file_list.txt";
            using (StreamWriter file = new StreamWriter(fileListPath))
            {
                foreach (string videoFile in videoFiles)
                {
                    file.WriteLine($"file '{videoFile}'");
                }
            }

            var outputTempPath = Path.Combine(Directory.GetCurrentDirectory(), projectGuid, $"outputTemp.mp4");
            outputTempPath = outputTempPath.Replace("\\", "/");

            ExecuteFfMpegCommand($"-f concat -safe 0 -i {fileListPath} -c copy \"{outputTempPath}\"");

            _logger.LogInformation($"Created FFMPEG image video full for {projectGuid}");

            var input1 = Path.Combine(Directory.GetCurrentDirectory(), projectGuid, $"{avatarVideoName}");
            var input2 = Path.Combine(Directory.GetCurrentDirectory(), projectGuid, $"outputTemp.mp4");
            input1 = input1.Replace("\\", "/");
            input2 = input2.Replace("\\", "/");
            ////ExecuteFfMpegCommand($"-i \"{input1}\" -i \"{input2}\" -filter_complex \"[1:v]scale=320:-1[ovrl]; [0:v][ovrl]overlay=W-w-10:10\" -codec:a copy output_pip.mp4"); PIP
            ///
            var outPutFinalpath = Path.Combine(Directory.GetCurrentDirectory(), projectGuid, $"{avatarVideoName}_side_by_side.mp4");
            outPutFinalpath = outPutFinalpath.Replace("\\", "/");

            // For Side-by-Side
            ExecuteFfMpegCommand($"-i \"{input1}\" -i \"{input2}\" -filter_complex \"[0:v][1:v]scale2ref=h=ih/1:w=iw/1[main][pip];[main][pip]hstack\" -codec:a copy \"{outPutFinalpath}\"");

            _logger.LogInformation($"Created FFMPEG combined full video for {projectGuid}");

            var output_video = Path.Combine(Directory.GetCurrentDirectory(), projectGuid, $"{avatarVideoName}_side_by_side.mp4");
            var output_video2 = Path.Combine(Directory.GetCurrentDirectory(), projectGuid, $"{avatarVideoName}_side_by_side_streamable.m3u8");

            output_video = output_video.Replace("\\", "/");
            output_video2 = output_video2.Replace("\\", "/");

            ConvertMp4ToHls(output_video, output_video2);

            var sasUrls = await UploadMatchingFilesToBlobAsync(Path.Combine(Directory.GetCurrentDirectory(), projectGuid), avatarVideoName);

            //var sas = await UploadToBlobAsync(output_video, avatarVideoName);

            _logger.LogInformation($"Uploaded sas for {projectGuid}");

            System.IO.File.Delete(input1);
            System.IO.File.Delete(input2);

            return sasUrls.First();

        }

        private async Task<List<string>> UploadMatchingFilesToBlobAsync(string directoryPath, string filePrefix)
        {
            List<string> sasUrls = new List<string>();

            // Ensure the directory exists
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Directory does not exist: {directoryPath}");
                return sasUrls;
            }

            // Search for files matching the pattern
            string searchPattern = $"{filePrefix}_side_by_side_streamable*";
            string[] files = Directory.GetFiles(directoryPath, searchPattern);

            foreach (string filePath in files)
            {
                var filePathLocal = filePath.Replace("\\", "/");
                string fileName = Path.GetFileName(filePathLocal);
                // Assuming UploadToBlobAsync is implemented to upload the file and return a SAS URL
                string sasUrl = await UploadToBlobAsync(filePathLocal, fileName);
                sasUrls.Add(sasUrl);
                Console.WriteLine($"Uploaded {fileName} and obtained SAS URL: {sasUrl}");
            }

            return sasUrls;
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

        private int convertDuration(string duration)
        {
            // Use a regular expression to extract minutes and seconds
            var match = Regex.Match(duration, @"PT(\d+)M(\d+(\.\d+)?)S");

            if (match.Success)
            {
                // Extract minutes and seconds from the duration string
                int minutes = int.Parse(match.Groups[1].Value);
                double seconds = double.Parse(match.Groups[2].Value);

                // Convert the entire duration to seconds
                double totalSeconds = minutes * 60 + seconds;

                return (int)Math.Ceiling(totalSeconds);
                
            }
            else
            {
                Console.WriteLine("The format of the input string is incorrect.");
                return 150;
            }
        }

        private async Task<string> uploadToFileShareAsync(string path, string fileName)
        {
            string shareEndpoint = "https://naanantestpicsel.file.core.windows.net";
            string shareName = "picsel-content";
            string folderName = "picsel";

            var shareClient = new ShareClient(_blobConfig.ConnectionString, shareName);

            // Get a reference to the directory
            var directoryClient = shareClient.GetDirectoryClient(folderName);

            // Ensure the directory exists
            await directoryClient.CreateIfNotExistsAsync();

            // Get a reference to the file client
            var fileClient = directoryClient.GetFileClient(fileName);


            // Open the file and upload its data
            const long chunkSize = 4 * 1024 * 1024; // 4 MiB, Azure's maximum range size
            using var stream = System.IO.File.OpenRead(path);
            long fileSize = stream.Length;

            await fileClient.CreateAsync(fileSize);

            long offset = 0;
            byte[] buffer = new byte[chunkSize];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                using MemoryStream ms = new MemoryStream(buffer, 0, bytesRead);
                await fileClient.UploadRangeAsync(new HttpRange(offset, bytesRead), ms);
                offset += bytesRead;
            }

            // Assuming _blobConfig.SasKey is a SAS token with permissions to access the file
            return $"{shareEndpoint}/{shareName}/{folderName}/{fileName}?{_blobConfig.SasKey}";
        }

        private async Task<string> UploadToBlobAsync(string path, string fileName)
        {
            string blobServiceEndpoint = "https://naanantestpicsel.blob.core.windows.net";
            string containerName = "picsel-test-blob";

            // Initialize the BlobServiceClient with your connection string
            var blobServiceClient = new BlobServiceClient(_blobConfig.ConnectionString);

            // Get a reference to the container
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Ensure the container exists
            await blobContainerClient.CreateIfNotExistsAsync();

            // Get a reference to the blob client
            var blobClient = blobContainerClient.GetBlobClient(fileName);

            // Open the file and upload its data to the blob
            using var uploadFileStream = System.IO.File.OpenRead(path);
            await blobClient.UploadAsync(uploadFileStream, overwrite: true);
            uploadFileStream.Close();

            // Assuming _blobConfig.SasKey is a SAS token with permissions to access the blob
            // Note: This assumes the SAS token is for blob access. Ensure your SAS token is appropriate for the operation.
            return $"{blobClient.Uri}?{_blobConfig.SasKey}";
        }

        [HttpGet("GetVideoForQuery")]
        public async Task<VideoContentResponse> GetVideoForQuery([FromQuery] string query, string fileSasUrl)
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

            var script = _azureAIHelper.GetQueryAnswer(query, fileContents);

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
        public async Task<string> GetTextAnswerForQuery(string query, string fileSasUrl, string targetLocale = "", string sourceLocale = "en-US")
        {
            try
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

                var script = _azureAIHelper.GetQueryAnswer(query, fileContents);
                //Convert it to desired locale
                if (!string.IsNullOrEmpty(targetLocale) && targetLocale != "en-US")
                    script = _azureAIHelper.GetLocalizedAnswerForQuery(script, targetLocale, sourceLocale);

                return script;
            }
            catch (Exception e)
            {
                throw new HttpRequestException("Unexpected", e, HttpStatusCode.ServiceUnavailable);
            }

            throw new HttpRequestException("Unexpected", new InvalidOperationException("Something went wrong please try again"), HttpStatusCode.ServiceUnavailable);
        }

        [HttpGet("GetVideoForQueryTopic")]
        public async Task<VideoContentResponse> GetVideoForQueryTopic([FromQuery] string query, string topicName)
        {
            var script = _azureAIHelper.GetQueryAnswer(query, topicName);

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

        [HttpGet("GetTextAnswerForQueryTopic")]
        public async Task<string> GetTextAnswerForQueryTopic(string query, string topicName, string targetLocale = "", string sourceLocale = "en-US")
        {
            var script = _azureAIHelper.GetQueryAnswer(query, topicName);
            //Convert it to desired locale
            if (!string.IsNullOrEmpty(targetLocale) && targetLocale != "en-US")
                script = _azureAIHelper.GetLocalizedAnswerForQuery(script, targetLocale, sourceLocale);

            return script;
        }
    }
}
