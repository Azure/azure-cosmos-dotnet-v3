//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Dedicated Gateway request options
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class DedicatedGatewayRequestOptions
    {
        /// <summary> 
        /// Gets or sets the staleness value associated with the request in the Azure CosmosDB service. 
        /// </summary> 
        /// <remarks> 
        /// For requests where the <see cref="ConsistencyLevel"/> is <see cref="ConsistencyLevel.Eventual"/>, responses from the integrated cache are guaranteed to be no staler than value indicated by this MaxIntegratedCacheStaleness. 
        /// Value defaults to null. Cache Stalenss is supported in milliseconds granularity. Anything smaller than milliseconds will be ignored.
        /// </remarks> 
        public TimeSpan? MaxIntegratedCacheStaleness { get; set; }

        internal static void PopulateMaxIntegratedCacheStalenessOption(DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions, RequestMessage request)
        {
            if (dedicatedGatewayRequestOptions?.MaxIntegratedCacheStaleness != null)
            {
                double cacheStalenessInMilliseconds = (double)dedicatedGatewayRequestOptions?.MaxIntegratedCacheStaleness.Value.TotalMilliseconds;
                request.Headers.Set(HttpConstants.HttpHeaders.DedicatedGatewayPerRequestCacheStaleness, cacheStalenessInMilliseconds.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}
