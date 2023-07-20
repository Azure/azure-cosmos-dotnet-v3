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
    public class DedicatedGatewayRequestOptions
    {
        /// <summary> 
        /// Gets or sets the staleness value associated with the request in the Azure CosmosDB service. 
        /// </summary> 
        /// <value>Default value is null.</value> 
        /// <remarks> 
        /// For requests where the <see cref="ConsistencyLevel"/> is <see cref="ConsistencyLevel.Eventual"/> or <see cref="ConsistencyLevel.Session"/>, responses from the integrated cache are guaranteed to be no staler than value indicated by this MaxIntegratedCacheStaleness. 
        /// Cache Staleness is supported in milliseconds granularity. Anything smaller than milliseconds will be ignored.
        /// </remarks> 
        public TimeSpan? MaxIntegratedCacheStaleness { get; set; }

        /// <summary>
        /// Gets or sets if bypass the integrated cache or not associated with the request in the Azure CosmosDB service.
        /// When set this value to true, the request will not be served from the integrated cache, and the response will not be cached either.
        /// </summary>
        /// <value>Default value is false.</value>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions
        /// {
        ///     BypassIntegratedCache = true
        /// };
        /// 
        /// // For ItemRequestOptions
        /// ItemRequestOptions requestOptions = new ItemRequestOptions
        /// {
        ///     DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
        /// };
        /// 
        /// // For QueryRequestOptions
        /// QueryRequestOptions requestOptions = new QueryRequestOptions
        /// {
        ///     DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
        /// };
        /// ]]>
        /// </code>
        /// </example>
#if PREVIEW
        public
#else
        internal
#endif
        bool? BypassIntegratedCache { get; set; }

        /// <summary>
        /// Get or set the shard key associated with the request in the Azure CosmosDB Service.
        /// </summary>
        /// <value>Default value is null</value>
        /// <remarks>
        /// Shard key is supported with the maximum length of 36 characters and accepts only alphanumeric characters and the hyphen (-) character.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions
        /// {
        ///     ShardKey = <shard_key>
        /// };
        /// 
        /// // For ItemRequestOptions
        /// ItemRequestOptions requestOptions = new ItemRequestOptions
        /// {
        ///     DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
        /// };
        /// 
        /// // For QueryRequestOptions
        /// QueryRequestOptions requestOptions = new QueryRequestOptions
        /// {
        ///     DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
        /// };
        /// ]]>
        /// </code>
        /// </example>
#if PREVIEW
        public
#else
        internal
#endif
        string ShardKey { get; set; }

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

        internal static void PopulateBypassIntegratedCacheOption(DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions, RequestMessage request)
        {
            if (dedicatedGatewayRequestOptions != null && dedicatedGatewayRequestOptions.BypassIntegratedCache.HasValue && dedicatedGatewayRequestOptions.BypassIntegratedCache.Value)
            {
                request.Headers.Set(HttpConstants.HttpHeaders.DedicatedGatewayPerRequestBypassIntegratedCache, true.ToString(CultureInfo.InvariantCulture));
            }
        }

        internal static void PopulateShardKeyOption(DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions, RequestMessage request)
        {
            if (!string.IsNullOrEmpty(dedicatedGatewayRequestOptions?.ShardKey))
            {
                request.Headers.Set(HttpConstants.HttpHeaders.DedicatedGatewayShardKey, dedicatedGatewayRequestOptions.ShardKey);
            }
        }
    }
}
