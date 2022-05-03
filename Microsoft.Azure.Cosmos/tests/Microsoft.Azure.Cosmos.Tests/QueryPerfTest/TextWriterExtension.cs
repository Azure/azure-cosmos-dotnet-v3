namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using System.IO;

    internal static class TextWriterExtension
    {
        private static readonly List<string> metricNames = new()
        {
            "RetrievedDocumentCount",
            "RetrievedDocumentSize",
            "OutputDocumentCount",
            "OutputDocumentSize",
            "TotalQueryExecutionTime",
            "DocumentLoadTime",
            "DocumentWriteTime",
            "Created",
            "ChannelAcquisitionStarted",
            "Pipelined",
            "TransitTime",
            "Received",
            "Completed",
            "PocoTime",
            "GetCosmosElementResponseTime",
            "EndToEndTime"
        };

        public static void WriteMetrics(this TextWriter textWriter, bool badRequestMetrics, int numberOfIterations, int roundTrips, params double[] metrics)
        {
            if (badRequestMetrics)
            {
                foreach (double metric in metrics)
                {
                    textWriter.WriteLine(metric);
                }
            }
            else
            {
                for (int i = 0; i < metrics.Length; i++)
                {
                    textWriter.WriteLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\"", "Iteration number: " + numberOfIterations, "RoundTrip: " + roundTrips, metricNames[i], metrics[i]));
                }
            }
        }
    }
}
