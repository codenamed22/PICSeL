# PICSeL

PICSeL is an ASP.NET Core Web API that generates short, AI-assisted educational videos from a topic, a content file, or a user query. It uses Azure OpenAI to summarize content and produce narration scripts, DALL-E to generate supporting visuals, Azure Speech Talking Avatar to synthesize avatar video, FFmpeg to assemble media, and Azure Storage to publish generated video assets.

## Features

- Generate a video from a topic name.
- Generate a video from a text/content file available through a SAS URL.
- Ask questions against uploaded content and receive either text or avatar-video responses.
- Translate uploaded videos into a target locale using Azure Speech video translation APIs.
- Generate visual prompts from scripts and compose a side-by-side avatar + visual video.
- Upload generated HLS output to Azure Blob Storage and return SAS-backed playback URLs.

## Tech stack

- .NET 8 / ASP.NET Core Web API
- Azure OpenAI chat completions
- Azure OpenAI image generation / DALL-E deployment
- Azure Speech Talking Avatar and Video Translation APIs
- Azure Key Vault for runtime secrets
- Azure Blob Storage and Azure Files
- Swagger / Swashbuckle
- FFmpeg

## Project structure

```text
PICSeL/
├── PICSeL.sln
└── PICSeL/
    ├── Controllers/
    │   └── VideoCreatorController.cs
    ├── Configs/
    │   ├── AzureAvatarConfig.cs
    │   ├── AzureSpeechServiceConfig.cs
    │   ├── BlobConfig.cs
    │   ├── DallEConfig.cs
    │   └── OpenAIConfig.cs
    ├── Models/
    │   ├── DallEResponse.cs
    │   ├── OpenAIRequest.cs
    │   ├── OpenAIResponse.cs
    │   ├── VideoContentResponse.cs
    │   └── VideoTranslationResponse.cs
    ├── Utils/
    │   ├── AzureAIHelper.cs
    │   ├── AzureSpeechHelper.cs
    │   ├── DallEHelper.cs
    │   └── TextSummarizationHelper.cs
    ├── Program.cs
    ├── appsettings.json
    └── PICSeL.csproj
```

## Prerequisites

Install the following before running the API locally:

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) or another identity supported by `DefaultAzureCredential`
- FFmpeg available on your `PATH`
- An Azure Key Vault
- Azure OpenAI chat completion deployment
- Azure OpenAI image generation deployment
- Azure Speech resource with Talking Avatar / Video Translation access
- Azure Storage account with Blob Storage access

Verify FFmpeg is installed:

```bash
ffmpeg -version
```

## Configuration

PICSeL reads non-secret configuration from `appsettings.json` and loads secrets from Azure Key Vault at startup.

Create or update `PICSeL/appsettings.Development.json` with your own endpoints:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "OpenAIConfig": {
    "Uri": "https://<openai-resource>.openai.azure.com/openai/deployments/<chat-deployment>/chat/completions?api-version=<api-version>",
    "BackupUri": "https://<backup-openai-resource>.openai.azure.com/openai/deployments/<backup-chat-deployment>/chat/completions?api-version=<api-version>"
  },
  "DallEConfig": {
    "Uri": "https://<openai-resource>.openai.azure.com/openai/deployments/<image-deployment>/images/generations?api-version=<api-version>"
  },
  "AzureAvatarConfig": {
    "Region": "<speech-region>",
    "Servicehost": "customvoice.api.speech.microsoft.com"
  },
  "KeyVault": "https://<your-key-vault-name>.vault.azure.net/"
}
```

Add the required secrets to Azure Key Vault:

```bash
az keyvault secret set --vault-name <vault-name> --name "OpenAIConfig--ApiKey" --value "<primary-azure-openai-key>"
az keyvault secret set --vault-name <vault-name> --name "OpenAIConfig--ApiKeyBackup" --value "<backup-azure-openai-key>"
az keyvault secret set --vault-name <vault-name> --name "AzureAvatarConfig--SubscriptionKey" --value "<azure-speech-key>"
az keyvault secret set --vault-name <vault-name> --name "AzureSpeechServiceConfig--ApiKey" --value "<azure-speech-key>"
az keyvault secret set --vault-name <vault-name> --name "ConnectionString" --value "<azure-storage-connection-string>"
az keyvault secret set --vault-name <vault-name> --name "SasKey" --value "<storage-sas-token-without-leading-question-mark>"
```

The identity used to run the application must have permission to read these secrets.

## Getting started

Clone the repository:

```bash
git clone https://github.com/codenamed22/PICSeL.git
cd PICSeL
```

Restore dependencies:

```bash
dotnet restore
```

Sign in to Azure for local Key Vault access:

```bash
az login
```

Run the API:

```bash
dotnet run --project PICSeL/PICSeL.csproj
```

The development profile serves the API at:

- `http://localhost:5041`
- `https://localhost:7241`

Swagger is available at:

```text
https://localhost:7241/swagger
```

## API endpoints

Base route:

```text
/api/picsel
```

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/GetVideoForTopic?topicName=<topic>` | Starts video generation for a topic and returns a job ID. |
| `GET` | `/GetVideoForContentFile?fileSasUrl=<url>` | Downloads a text file from a SAS URL, summarizes it, and starts video generation. |
| `GET` | `/GetVideoForQuery?query=<query>&fileSasUrl=<url>` | Answers a query against a file and returns an avatar-video response. |
| `GET` | `/GetTextAnswerForQuery?query=<query>&fileSasUrl=<url>&targetLocale=<locale>&sourceLocale=<locale>` | Answers a query against a file and optionally localizes the text response. |
| `GET` | `/GetVideoForQueryTopic?query=<query>&topicName=<topic>` | Answers a query against a topic and returns an avatar-video response. |
| `GET` | `/GetTextAnswerForQueryTopic?query=<query>&topicName=<topic>&targetLocale=<locale>&sourceLocale=<locale>` | Answers a query against a topic and optionally localizes the text response. |
| `POST` | `/UploadAndTranslateVideo?videoBlobUri=<url>&inputLocale=<locale>&targetLocale=<locale>` | Uploads a source video to Azure Speech video translation and returns the translated video URL when processing succeeds. |

## Example requests

Generate a video from a topic:

```bash
curl "https://localhost:7241/api/picsel/GetVideoForTopic?topicName=water%20cycle"
```

Generate a text answer from a file:

```bash
curl "https://localhost:7241/api/picsel/GetTextAnswerForQuery?query=Summarize%20the%20main%20idea&fileSasUrl=<encoded-file-sas-url>"
```

Generate a localized answer:

```bash
curl "https://localhost:7241/api/picsel/GetTextAnswerForQueryTopic?query=Explain%20this%20simply&topicName=photosynthesis&targetLocale=hi-IN&sourceLocale=en-US"
```

Translate a video:

```bash
curl -X POST "https://localhost:7241/api/picsel/UploadAndTranslateVideo?videoBlobUri=<encoded-video-blob-url>&inputLocale=en-US&targetLocale=hi-IN"
```

## Media generation flow

1. User submits a topic, content file, or query.
2. Azure OpenAI generates a summary, answer, narration script, or SSML.
3. Visual prompts are extracted from script sections that match `[Cut to visual: ...]`.
4. DALL-E generates images for each extracted visual prompt.
5. Azure Speech Talking Avatar generates the avatar video from SSML.
6. FFmpeg converts images into timed video clips and combines them with the avatar video.
7. FFmpeg converts the final MP4 output to HLS.
8. Generated HLS files are uploaded to Azure Blob Storage.
9. The API returns a SAS URL for the generated output.

## Development notes

- Generated media is written to a temporary project folder named with a GUID.
- FFmpeg must be installed and accessible as `ffmpeg` from the application process.
- The video-generation endpoint currently returns a job ID immediately, while media creation continues asynchronously.
- There is no dedicated job-status endpoint yet; add one before using asynchronous generation in production.
- Avoid committing real service endpoints, storage account names, vault names, keys, or SAS tokens.
- Use separate Azure resources and storage containers for development, staging, and production.

## Troubleshooting

### Key Vault authentication fails

Run `az login`, confirm the `KeyVault` URL is correct, and verify your user or managed identity can read secrets.

### FFmpeg commands fail

Confirm FFmpeg is installed and available on `PATH`:

```bash
ffmpeg -version
```

### Avatar synthesis fails

Check that the Azure Speech key, region, and Talking Avatar endpoint are valid. Also verify the generated SSML is valid XML.

### Blob upload fails

Check the storage connection string, container permissions, and SAS token permissions.

## Roadmap ideas

- Add a job-status endpoint for long-running video generation.
- Move hardcoded storage container names into configuration.
- Add structured request and response DTOs for each endpoint.
- Add validation for query parameters and SAS URLs.
- Add unit tests for prompt generation, SSML validation, and FFmpeg command construction.
- Add CI/CD with build and test checks.

## License

No license file is currently included. Add a `LICENSE` file before distributing or accepting external contributions.
