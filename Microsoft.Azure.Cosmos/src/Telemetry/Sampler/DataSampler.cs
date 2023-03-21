//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Telemetry.Models;

    /// <summary>
    /// Sampler to select top N unique records and return true/false on the basis of elements already selected.
    /// </summary>
    internal sealed class DataSampler
    {
        public static List<RequestInfo> SampleOrderByP99(List<RequestInfo> requestInfoList)
        {
            return requestInfoList.GroupBy(r => new
            {
                r.DatabaseName,
                r.ContainerName,
                r.Operation,
                r.Resource,
                r.StatusCode,
                r.SubStatusCode
            })
             .SelectMany(g => g.OrderByDescending(r => r.Metrics.FirstOrDefault(m => m.MetricsName == ClientTelemetryOptions.RequestLatencyName)?.Percentiles[ClientTelemetryOptions.Percentile99])
                                .Take(ClientTelemetryOptions.NetworkRequestsSampleSizeThrehold)).ToList();
        }

        public static List<RequestInfo> SampleOrderByCount(List<RequestInfo> requestInfoList)
        {
            return requestInfoList.GroupBy(r => new
            {
                r.DatabaseName,
                r.ContainerName,
                r.Operation,
                r.Resource,
                r.StatusCode,
                r.SubStatusCode
            })
             .SelectMany(g => g.OrderByDescending(r => r.Metrics.FirstOrDefault(m => m.MetricsName == ClientTelemetryOptions.RequestLatencyName)?.Count)
                                .Take(ClientTelemetryOptions.NetworkRequestsSampleSizeThrehold)).ToList();
        }
    }
}
