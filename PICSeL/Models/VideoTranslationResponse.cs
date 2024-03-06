using System.Net;

namespace PICSeL.Models
{
    public class VideoTranslationResponse
    {
        public class UploadResponse
        {
            public Uri Self { get; set; }
        }

        public class TranslateResponse
        {
            public Uri Self { get; set; }
            public Guid VideoFileId { get; set; }
        }

        public class QueryResponse
        {
            public Dictionary<string, Dictionary<string, string>> TargetLocales { get; set; }
            public string Status { get; set; }
        }

        public class Response
        {
            public HttpStatusCode StatusCode { get; set; }
            public string Uri { get; set; }
        }
    }
}
