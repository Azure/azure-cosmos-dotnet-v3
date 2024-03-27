// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System.Collections.Generic;

    internal static class IndexUtilizationHelper
    {
        public static IReadOnlyDictionary<string, string> AccumulateIndexUtilization(
            IReadOnlyDictionary<string, string> cumulativeHeaders,
            IReadOnlyDictionary<string, string> currentHeaders)
        {
            if (cumulativeHeaders == null)
            {
                return currentHeaders;
            }

            if (currentHeaders == null)
            {
                return cumulativeHeaders;
            }

            // Index utilization is supposed to be static across partitions and round trips.
            if (currentHeaders.ContainsKey(Documents.HttpConstants.HttpHeaders.IndexUtilization) ||
                !cumulativeHeaders.ContainsKey(Documents.HttpConstants.HttpHeaders.IndexUtilization))
            {
                return currentHeaders;
            }

            Dictionary<string, string> additionalHeaders;
            if (currentHeaders is Dictionary<string, string> currentHeadersDictionary)
            {
                additionalHeaders = currentHeadersDictionary;
            }
            else
            {
                // Until we get the new .NET version, we need to copy the headers manually.
                additionalHeaders = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> header in currentHeaders)
                {
                    additionalHeaders.Add(header.Key, header.Value);
                }
            }

            additionalHeaders.Add(
                Documents.HttpConstants.HttpHeaders.IndexUtilization,
                cumulativeHeaders[Documents.HttpConstants.HttpHeaders.IndexUtilization]);

            return additionalHeaders;
        }
    }
}