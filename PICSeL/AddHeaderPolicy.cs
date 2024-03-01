using Azure.Core.Pipeline;
using Azure.Core;

namespace PICSeL
{
    public class AddHeaderPolicy : HttpPipelinePolicy
    {
        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            message.Request.Headers.Add("x-ms-file-request-intent", "YOUR_INTENDED_VALUE");
            ProcessNext(message, pipeline);
        }

        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            message.Request.Headers.Add("x-ms-file-request-intent", "YOUR_INTENDED_VALUE");
            return ProcessNextAsync(message, pipeline);
        }
    }
}
