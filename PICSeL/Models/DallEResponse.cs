namespace PICSeL.Models
{
    using System;
    using System.Collections.Generic;
    using System.Net;

    public class FilterResult
    {
        public bool Filtered { get; set; }
        public string Severity { get; set; }
        public bool? Detected { get; set; } // Optional, used for "profanity" which has an additional field
    }

    public class ContentFilterResults
    {
        public FilterResult Hate { get; set; }
        public FilterResult SelfHarm { get; set; }
        public FilterResult Sexual { get; set; }
        public FilterResult Violence { get; set; }
    }

    public class PromptFilterResults
    {
        public FilterResult Hate { get; set; }
        public FilterResult Profanity { get; set; }
        public FilterResult SelfHarm { get; set; }
        public FilterResult Sexual { get; set; }
        public FilterResult Violence { get; set; }
    }

    public class DataItem
    {
        public ContentFilterResults ContentFilterResults { get; set; }
        public PromptFilterResults PromptFilterResults { get; set; }
        public string RevisedPrompt { get; set; }
        public string Url { get; set; }
    }

    public class DallEResponse
    {
        public long Created { get; set; }
        public List<DataItem> Data { get; set; }
        public HttpStatusCode Status { get; set; }
    }

}
