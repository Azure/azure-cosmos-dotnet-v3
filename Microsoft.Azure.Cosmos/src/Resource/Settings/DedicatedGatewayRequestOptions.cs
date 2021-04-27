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
        /// <value>Default value is null.</value> 
        /// <remarks> 
        /// For requests where the <see cref="ConsistencyLevel"/> is <see cref="ConsistencyLevel.Eventual"/>, responses from the integrated cache are guaranteed to be no staler than value indicated by this MaxIntegratedCacheStaleness. 
        /// Cache Staleness is supported in milliseconds granularity. Anything smaller than milliseconds will be ignored.
        /// </remarks> 
        public TimeSpan? MaxIntegratedCacheStaleness { get; set; }

        internal static void PopulateMaxIntegratedCacheStalenessOption(DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions, RequestMessage request)
        {
            if (dedicatedGatewayRequestOptions?.MaxIntegratedCacheStaleness != null)
            {
                double cacheStalenessInMilliseconds = (double)dedicatedGatewayRequestOptions.MaxIntegratedCacheStaleness.Value.TotalMilliseconds; 

                if (cacheStalenessInMilliseconds < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(DedicatedGatewayRequestOptions.MaxIntegratedCacheStaleness), "MaxIntegratedCacheStaleness cannot be negative.");
                }

                request.Headers.Set(HttpConstants.HttpHeaders.DedicatedGatewayPerRequestCacheStaleness, cacheStalenessInMilliseconds.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}
