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

    /// <summary>
    /// Sampler to select top N unique records and return true/false on the basis of elements already selected.
    /// </summary>
    internal sealed class DataSampler
    {
        public static List<RequestInfo> OrderAndSample(List<RequestInfo> requestInfoList, DataSamplerOrderBy orderBy)
        {
            // It will store final result
            List<RequestInfo> sampledData = new List<RequestInfo>(capacity: requestInfoList.Count);

            // Processing (Grouping, Sorting will happen in this collection)
            IDictionary<int, List<KeyValuePair<double, RequestInfo>>> sampledRawData = new Dictionary<int, List<KeyValuePair<double, RequestInfo>>>();

            foreach (RequestInfo requestInfo in requestInfoList)
            {
                int key = requestInfo.GetHashCodeForSampler();

                // Check if similar object is already present
                if (sampledRawData.TryGetValue(key, out List<KeyValuePair<double, RequestInfo>> sortedData))
                {
                    DataSampler.AddToList(orderBy, requestInfo, sortedData);

                    sortedData.Sort(DataComparer.Instance);

                    if (sortedData.Count > ClientTelemetryOptions.NetworkRequestsSampleSizeThreshold)
                    {
                        sortedData.RemoveAt(sortedData.Count - 1);
                    }
                    sampledRawData.Remove(key);
                    sampledRawData.Add(key, sortedData);
                }
                else
                {
                    // Create a new list of KeyValue pair where we will be sorting this list by the key and Value is original Request Info object
                    // In this case key can be duplicated as latency and samplecount can be same for different scenario, hence using KeyValuePair to store this info
                    List<KeyValuePair<double, RequestInfo>> newSortedData 
                        = new List<KeyValuePair<double, RequestInfo>>(ClientTelemetryOptions.NetworkRequestsSampleSizeThreshold + 1);

                    DataSampler.AddToList(orderBy, requestInfo, newSortedData);

                    sampledRawData.Add(key, newSortedData);
                }
            }

            foreach (List<KeyValuePair<double, RequestInfo>> sampledRequestInfo in sampledRawData.Values)
            {
                foreach (KeyValuePair<double, RequestInfo> pair in sampledRequestInfo)
                {
                    sampledData.Add(pair.Value);
                }
            }

            return sampledData;
        }

        private static void AddToList(DataSamplerOrderBy orderBy, RequestInfo requestInfo, List<KeyValuePair<double, RequestInfo>> sortedData)
        {
            if (orderBy == DataSamplerOrderBy.Latency)
            {
                sortedData.Add(new KeyValuePair<double, RequestInfo>(requestInfo.GetP99Latency(), requestInfo));
            }
            else if (orderBy == DataSamplerOrderBy.SampleCount)
            {
                sortedData.Add(new KeyValuePair<double, RequestInfo>(requestInfo.GetSampleCount(), requestInfo));
            }
            else
            {
                throw new Exception("order by not supported. Only Supported values are Latency, SampleCount");
            }
        }
    }

    internal class DataComparer : IComparer<KeyValuePair<double, RequestInfo>>
    {
        public static DataComparer Instance = new DataComparer();
        public int Compare(KeyValuePair<double, RequestInfo> a, KeyValuePair<double, RequestInfo> b)
        {
            return b.Key.CompareTo(a.Key);
        }
    }

    internal enum DataSamplerOrderBy
    {
        Latency, 
        SampleCount
    }
}
