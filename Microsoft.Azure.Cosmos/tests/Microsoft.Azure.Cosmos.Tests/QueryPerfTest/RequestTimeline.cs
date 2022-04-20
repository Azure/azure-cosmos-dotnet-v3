namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal class RequestTimeline
    {
        public DateTime StartTimeUtc { get; set; }

        public EventType Event { get; set; }

        public double DurationInMs { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum EventType
        {
            Created,
            ChannelAcquisitionStarted,
            Pipelined,
            [EnumMember(Value = "Transit Time")]
            TransitTime,
            Received,
            Completed
        }
    }
}
