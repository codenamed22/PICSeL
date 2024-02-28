using System.Net;

namespace PICSeL.Models
{
    public class OpenAIResponse
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public long Created { get; set; }
        public string Model { get; set; }
        public Choice[] Choices { get; set; }
        public Usage Usage { get; set; }
        public HttpStatusCode Status { get; set; }
    }

    public class Choice
    {
        public int Index { get; set; }
        public string FinishReason { get; set; }
        public Message Message { get; set; }
    }

    public class Usage
    {
        public int CompletionTokens { get; set; }
        public int PromptTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
