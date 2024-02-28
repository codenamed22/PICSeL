namespace PICSeL.Models
{
    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class OpenAIRequest
    {
        public List<Message> messages { get; set; }
        public double temperature { get; set; }
        public double top_p { get; set; }
        public double frequency_penalty { get; set; }
        public double presence_penalty { get; set; }
        public int max_tokens { get; set; }
        public object stop { get; set; }
        public bool stream { get; set; }
    }
}
