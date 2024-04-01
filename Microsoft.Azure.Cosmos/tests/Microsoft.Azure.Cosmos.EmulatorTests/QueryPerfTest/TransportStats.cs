namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Collections.Generic;

    internal class TransportStats
    {
        public List<RequestTimeline> RequestTimeline { get; } = new();
    }
}
