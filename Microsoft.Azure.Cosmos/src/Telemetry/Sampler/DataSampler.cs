//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Sampler to select top N unique records and return true/false on the basis of elements already selected.
    /// </summary>
    internal sealed class DataSampler
    {
        public static List<RequestInfo> OrderAndSample(List<RequestInfo> requestInfoList, IComparer<RequestInfo> comparer, NetworkTelemetryConfig config)
        {
            // It will store final result
            List<RequestInfo> sampledData = new List<RequestInfo>(capacity: requestInfoList.Count);

            // Processing (Grouping, Sorting will happen in this collection)
            IDictionary<int, List<RequestInfo>> sampledRawData = new Dictionary<int, List<RequestInfo>>();

            foreach (RequestInfo requestInfo in requestInfoList)
            {
                // Get a unique key identifier for an object
                int key = requestInfo.GetHashCodeForSampler();
                
                // Check if similar object is already present otherwise create a new list and add
                if (sampledRawData.TryGetValue(key, out List<RequestInfo> groupedData))
                {
                    groupedData.Add(requestInfo);
                    sampledRawData[key] = groupedData;
                }
                else
                {
                    sampledRawData.Add(key, new List<RequestInfo>() { requestInfo });
                }
            }
            
            // If list is greater than threshold then sort it and get top N objects otherwise add list as it is
            foreach (List<RequestInfo> sampledRequestInfo in sampledRawData.Values)
            {
                if (sampledRequestInfo.Count > config.NetworkRequestsSampleSizeThreshold)
                {
                    sampledRequestInfo.Sort(comparer);
                    sampledData.AddRange(sampledRequestInfo.GetRange(
                        index: 0,
                        count: config.NetworkRequestsSampleSizeThreshold));
                }
                else
                {
                    sampledData.AddRange(sampledRequestInfo);
                }
            }

            return sampledData;
        }
        
    }
    
    internal class DataLatencyComparer : IComparer<RequestInfo>
    {
        public static DataLatencyComparer Instance = new DataLatencyComparer();
        public int Compare(RequestInfo a, RequestInfo b)
        {
            return b.GetP99Latency().CompareTo(a.GetP99Latency());
        }
    }

    internal class DataSampleCountComparer : IComparer<RequestInfo>
    {
        public static DataSampleCountComparer Instance = new DataSampleCountComparer();
        public int Compare(RequestInfo a, RequestInfo b)
        {
            return b.GetSampleCount().CompareTo(a.GetSampleCount());
        }
    }
    
}
