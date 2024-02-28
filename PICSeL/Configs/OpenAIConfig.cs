namespace PICSeL.Configs
{
    public class OpenAIConfig
    {
        public string ApiKey { get; set; }

        public string ApiKeyBackup { get; set; }

        public Uri Uri { get; set; }

        public Uri BackupUri { get; set; }
    }
}
