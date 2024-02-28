using Azure;
using Azure.AI.TextAnalytics;

namespace PICSeL.Utils
{
    public class TextSummarizationHelper
    {
        // This example requires environment variables named "LANGUAGE_KEY" and "LANGUAGE_ENDPOINT"
        static string languageKey = "";
        static string languageEndpoint = "https://naanan-picsel-summarizer.cognitiveservices.azure.com/";

        private static readonly AzureKeyCredential credentials = new AzureKeyCredential(languageKey);
        private static readonly Uri endpoint = new Uri(languageEndpoint);

        // Example method for summarizing text
        public static async Task TextSummarizationExample(TextAnalyticsClient client)
        {
            string document = @"What is the Water Cycle? 
                The water cycle is a way that water moves all around the Earth. It never stops, it does 
                not have a beginning or an end. It's like a big circle! 
                Did you know?
                 The water cycle is also known as the “hydrologic cycle” 
                 Earth has been recycling water for over 4 billion years!
                 Do plants sweat? Well, sort of.... People perspire (sweat) and plants transpire. 
                Transpiration is the process by which plants lose water out of their leaves. 
                So how can we understand this magical process called the water cycle? 
                There are four main parts to the water cycle: Evaporation, Convection, Precipitation 
                and Collection.
                1) Evaporation
                Evaporation is when the sun heats up water in rivers or lakes or the ocean and turns 
                it into vapour or steam. The water vapour or steam leaves the river, lake or ocean 
                and goes into the air.
                We have already learnt about transpiration, did you know that transpiration gives 
                evaporation a bit of a hand in getting the water vapour back up into the air!
                2) Convection
                Convection in the water cycle is when the air near the surface is heated, then rises 
                taking heat with it. Water vapour in the air gets cold and changes back into liquid, 
                forming clouds. This is called condensation.
                You can see the same sort of thing at home... Pour a glass of cold water on a hot day 
                and watch what happens. Water forms on the outside of the glass. That water 
                didn't somehow leak through the glass! It actually came from the air. Water vapour 
                in the warm air, turns back into liquid when it touches the cold glass!
                3) Precipitation 
                Precipitation occurs when so much water has condensed that the air cannot hold it 
                anymore. The clouds get heavy and water falls back to the earth in the form of rain, 
                hail, sleet or snow.
                4) Collection/Storage
                A lot of the Earth's water does not take part in the water cycle very often. Much of it 
                is stored. The Earth stores water in a number of places. The ocean is the largest 
                storage of water. Around 96% of the Earth's water is stored in the ocean. We can't 
                drink the salty ocean water, so fortunately for us, freshwater is also stored in lakes, 
                glaciers, snow caps, rivers, and below the ground in groundwater storage";

            // Prepare analyze operation input. You can add multiple documents to this list and perform the same
            // operation to all of them.
            var batchInput = new List<string>
            {
                document
            };

            TextAnalyticsActions actions = new TextAnalyticsActions()
            {
                ExtractiveSummarizeActions = new List<ExtractiveSummarizeAction>() { new ExtractiveSummarizeAction() }
            };

            // Start analysis process.
            AnalyzeActionsOperation operation = await client.StartAnalyzeActionsAsync(batchInput, actions);
            await operation.WaitForCompletionAsync();
            // View operation status.
            Console.WriteLine($"AnalyzeActions operation has completed");
            Console.WriteLine();

            Console.WriteLine($"Created On   : {operation.CreatedOn}");
            Console.WriteLine($"Expires On   : {operation.ExpiresOn}");
            Console.WriteLine($"Id           : {operation.Id}");
            Console.WriteLine($"Status       : {operation.Status}");

            Console.WriteLine();
            // View operation results.
            await foreach (AnalyzeActionsResult documentsInPage in operation.Value)
            {
                IReadOnlyCollection<ExtractiveSummarizeActionResult> summaryResults = documentsInPage.ExtractiveSummarizeResults;

                foreach (ExtractiveSummarizeActionResult summaryActionResults in summaryResults)
                {
                    if (summaryActionResults.HasError)
                    {
                        Console.WriteLine($"  Error!");
                        Console.WriteLine($"  Action error code: {summaryActionResults.Error.ErrorCode}.");
                        Console.WriteLine($"  Message: {summaryActionResults.Error.Message}");
                        continue;
                    }

                    foreach (ExtractiveSummarizeResult documentResults in summaryActionResults.DocumentsResults)
                    {
                        if (documentResults.HasError)
                        {
                            Console.WriteLine($"  Error!");
                            Console.WriteLine($"  Document error code: {documentResults.Error.ErrorCode}.");
                            Console.WriteLine($"  Message: {documentResults.Error.Message}");
                            continue;
                        }

                        Console.WriteLine($"  Extracted the following {documentResults.Sentences.Count} sentence(s):");
                        Console.WriteLine();

                        foreach (var sentence in documentResults.Sentences)
                        {
                            Console.WriteLine($"  Sentence: {sentence.Text}");
                            Console.WriteLine();
                        }
                    }
                }
            }

        }

        static async Task Main(string[] args)
        {
            var client = new TextAnalyticsClient(endpoint, credentials);
            await TextSummarizationExample(client);
        }
    }
}
