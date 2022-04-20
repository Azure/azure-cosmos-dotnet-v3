namespace Microsoft.Azure.Cosmos.Tests
{
    internal class QueryMetrics
    {
        public double EndToEndTime { get; set; }

        public double PocoTime { get; set; }

        public double GetCosmosElementResponseTime { get; set; }

        public double RetrievedDocumentCount { get; set; }

        public double RetrievedDocumentSize { get; set; }

        public double OutputDocumentCount { get; set; }

        public double OutputDocumentSize { get; set; }

        public double TotalQueryExecutionTime { get; set; }

        public double DocumentLoadTime { get; set; }

        public double DocumentWriteTime { get; set; }

        public double Created { get; set; }

        public double ChannelAcquisitionStarted { get; set; }

        public double Pipelined { get; set; }

        public double TransitTime { get; set; }

        public double Received { get; set; }

        public double Completed { get; set; }

        public double BadRequestCreated { get; set; }

        public double BadRequestChannelAcquisitionStarted { get; set; }

        public double BadRequestPipelined { get; set; }

        public double BadRequestTransitTime { get; set; }

        public double BadRequestReceived { get; set; }

        public double BadRequestCompleted { get; set; }
    }
}