using System;
using System.Collections.Generic;

namespace PICSeL.Models
{
    public class VideoContentResponse
    {
        public string TextType { get; set; }
        public CustomVoices CustomVoices { get; set; }
        public Properties Properties { get; set; }
        public Outputs Outputs { get; set; }
        public DateTime LastActionDateTime { get; set; }
        public string Status { get; set; }
        public string Id { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    public class CustomVoices
    {
        // Depending on the structure, you may need to add properties here
    }

    public class Properties
    {
        public int AudioSize { get; set; }
        public long DurationInTicks { get; set; }
        public int SucceededAudioCount { get; set; }
        public string Duration { get; set; }
        public BillingDetails BillingDetails { get; set; }
        public string TimeToLive { get; set; }
        public string OutputFormat { get; set; }
        public string TalkingAvatarCharacter { get; set; }
        public string TalkingAvatarStyle { get; set; }
        public int KBitrate { get; set; }
        public string VideoFormat { get; set; }
        public string VideoCodec { get; set; }
        public string SubtitleType { get; set; }
        public string BackgroundColor { get; set; }
        public bool Customized { get; set; }
    }

    public class BillingDetails
    {
        public int CustomNeural { get; set; }
        public int Neural { get; set; }
    }

    public class Outputs
    {
        public string Result { get; set; }
        public string Summary { get; set; }
    }

}
