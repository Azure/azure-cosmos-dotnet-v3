//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    internal class NetworkMetricData
    {
        // Constructor
        public NetworkMetricData(
            double latency,
            long? requestBodySize,
            long? responseBodySize,
            double backendLatency,
            double? channelAcquisitionLatency,
            double? transitTimeLatency,
            double? receivedLatency)
        {
            this.Latency = latency;
            this.RequestBodySize = requestBodySize;
            this.ResponseBodySize = responseBodySize;
            this.BackendLatency = backendLatency;
            this.ChannelAcquisitionLatency = channelAcquisitionLatency;
            this.TransitTimeLatency = transitTimeLatency;
            this.ReceivedLatency = receivedLatency;
        }

        // Constructor
        public NetworkMetricData(
            double latency,
            long? requestBodySize,
            long? responseBodySize)
        {
            this.Latency = latency;
            this.RequestBodySize = requestBodySize;
            this.ResponseBodySize = responseBodySize;
        }

        public double Latency { get; }
        public long? RequestBodySize { get; }
        public long? ResponseBodySize { get; }
        public double BackendLatency { get; }
        public double? ChannelAcquisitionLatency { get; }
        public double? TransitTimeLatency { get; }
        public double? ReceivedLatency { get; }
    }
}
