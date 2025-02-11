//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The default cosmos request options
    /// </summary>
    public class RequestOptions
    {
        /// <summary>
        /// Gets or sets the If-Match (ETag) associated with the request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Most commonly used with the Delete* and Replace* methods of <see cref="Container"/> such as <see cref="Container.ReplaceItemAsync{T}(T, string, PartitionKey?, ItemRequestOptions, System.Threading.CancellationToken)"/>.
        /// <see cref="Container.CreateItemAsync{T}(T, PartitionKey?, ItemRequestOptions, System.Threading.CancellationToken)"/> will ignore <see cref="IfMatchEtag"/> if specificed. 
        /// <see cref="Container.UpsertItemAsync{T}(T, PartitionKey?, ItemRequestOptions, System.Threading.CancellationToken)"/> will ignore <see cref="IfMatchEtag"/> when materialized as Create, otherwise for Replace Etag constraint will be applied.
        /// 
        /// <seealso href="https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/database-transactions-optimistic-concurrency#implementing-optimistic-concurrency-control-using-etag-and-http-headers"/>
        /// </remarks>
        public string IfMatchEtag { get; set; }

        /// <summary>
        /// Most commonly used to detect changes to the resource
        /// Gets or sets the If-None-Match (ETag) associated with the request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Most commonly used with reads such as <see cref="Container.ReadItemAsync{T}(string, PartitionKey, ItemRequestOptions, System.Threading.CancellationToken)"/>.
        /// When Item Etag matches the specified <see cref="IfNoneMatchEtag"/> then 304 status code will be returned, otherwise existing Item will be returned with 200.
        /// <see cref="Container.UpsertItemAsync{T}(T, PartitionKey?, ItemRequestOptions, System.Threading.CancellationToken)"/> will ignore <see cref="IfNoneMatchEtag"/> when materialized as Create, otherwise for Replace Etag constraint will be applied.
        /// 
        /// To match any Etag use "*"
        /// If specified for writes (ex: Create, Replace, Delete) will be ignored.
        /// 
        /// <seealso href="https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/database-transactions-optimistic-concurrency#implementing-optimistic-concurrency-control-using-etag-and-http-headers"/>
        /// </remarks>
        public string IfNoneMatchEtag { get; set; }

        /// <summary>
        /// Application opted Cosmos request context that flow through with the <see cref="RequestMessage" />.
        /// Context will be available through handlers.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties { get; set; }

        /// <summary>
        /// Gets or sets a delegate which injects/appends a custom header in the request.
        /// </summary>
        public Action<Headers> AddRequestHeaders { get; set; }

        /// <summary>
        /// Gets or sets the priority level for a request.
        /// </summary>
        /// <remarks>
        /// Setting priority level only has an effect if Priority Based Execution is enabled.
        /// If it is not enabled, the priority level is ignored by the backend.
        /// If <see cref="CosmosClientOptions.AllowBulkExecution"/> is set to true on CosmosClient, priority level set in RequestOptions is ignored.
        /// Default PriorityLevel for each request is treated as High. It can be explicitly set to Low for some requests.
        /// When Priority based execution is enabled, if there are more requests than the configured RU/S in a second, 
        /// then Cosmos DB will throttle low priority requests to allow high priority requests to execute.
        /// This does not limit the throughput available to each priority level. Each priority level can consume the complete
        /// provisioned throughput in absence of the other. If both priorities are present and the user goes above the
        /// configured RU/s, low priority requests start getting throttled first to allow execution of mission critical workloads.
        /// </remarks>
        /// <seealso href="https://aka.ms/CosmosDB/PriorityBasedExecution"/>
        public PriorityLevel? PriorityLevel { get; set; }

        /// <summary>
        /// Threshold values for Distributed Tracing. 
        /// These values decides whether to generate operation level <see cref="System.Diagnostics.Tracing.EventSource"/> with request diagnostics or not.
        /// </summary>
        public CosmosThresholdOptions CosmosThresholdOptions { get; set; }

        /// <summary>
        /// List of regions to be excluded routing the request to.
        /// This can be used to route a request to a specific region by excluding all other regions.
        /// If all regions are excluded, then the request will be routed to the primary/hub region.
        /// </summary>
        public List<string> ExcludeRegions { get; set; }

        /// <summary>
        /// Cosmos availability strategy.
        /// Availability strategy allows the SDK to send out additional cross region requests to help 
        /// reduce latency and increase availability. Currently there is one type of availability strategy, parallel request hedging.
        /// If there is a globally enabled availability strategy, setting one in the request options will override the global one.
        /// </summary>
#if PREVIEW
        public
#else
        internal
#endif
        AvailabilityStrategy AvailabilityStrategy { get; set; }

        /// <summary>
        /// Gets or sets the boolean to use effective partition key routing in the cosmos db request.
        /// </summary>
        internal bool IsEffectivePartitionKeyRouting { get; set; }

        /// <summary>
        /// Gets or sets the consistency level required for the request in the Azure Cosmos DB service.
        /// Not every request supports consistency level. This allows each child to decide to expose it
        /// and use the same base logic
        /// </summary>
        /// <value>
        /// The consistency level required for the request.
        /// </value>
        /// <remarks>
        /// ConsistencyLevel compatibility will validated and set by RequestInvokeHandler
        /// </remarks>
        internal virtual ConsistencyLevel? BaseConsistencyLevel { get; set; }

        internal bool DisablePointOperationDiagnostics { get; set; }

        /// <summary>
        /// Gets or sets the throughput bucket for a request.
        /// </summary>
        /// <remarks>
        /// If <see cref="CosmosClientOptions.AllowBulkExecution"/> is set to true on CosmosClient,
        /// <see cref="RequestOptions.ThroughputBucket"/> cannot be set in RequestOptions.
        /// </remarks>
        /// <seealso href="https://aka.ms/cosmsodb-bucketing"/>
        internal int? ThroughputBucket { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal virtual void PopulateRequestOptions(RequestMessage request)
        {
            if (this.Properties != null)
            {
                foreach (KeyValuePair<string, object> property in this.Properties)
                {
                    request.Properties[property.Key] = property.Value;
                }
            }

            if (this.IfMatchEtag != null)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.IfMatch, this.IfMatchEtag);
            }

            if (this.IfNoneMatchEtag != null)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.IfNoneMatch, this.IfNoneMatchEtag);
            }

            if (this.PriorityLevel.HasValue)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PriorityLevel, this.PriorityLevel.ToString());
            }

            if (this.ThroughputBucket.HasValue)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.ThroughputBucket, this.ThroughputBucket?.ToString(CultureInfo.InvariantCulture));
            }

            this.AddRequestHeaders?.Invoke(request.Headers);
        }

        /// <summary>
        /// Clone RequestOptions.
        /// </summary>
        /// <returns> cloned RequestOptions. </returns>
        public RequestOptions ShallowCopy()
        {
            return this.MemberwiseClone() as RequestOptions;
        }

        /// <summary>
        /// Gets the resource URI passed in as a request option. This is used by MongoDB and Cassandra implementation for performance reasons.
        /// </summary>
        /// <param name="resourceUri">The URI passed in from the request options</param>
        /// <returns>True if the object exists in the request options. False if the value was not passed in as a request option</returns>
        internal bool TryGetResourceUri(out Uri resourceUri)
        {
            if (this.Properties != null && this.Properties.TryGetValue(HandlerConstants.ResourceUri, out object requestOptesourceUri))
            {
                Uri uri = requestOptesourceUri as Uri;
                if (uri == null || uri.IsAbsoluteUri)
                {
                    throw new ArgumentException(HandlerConstants.ResourceUri + " must be a relative Uri of type System.Uri");
                }

                resourceUri = uri;
                return true;
            }

            resourceUri = null;
            return false;
        }

        /// <summary>
        /// Set the session token
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <param name="sessionToken">The current session token.</param>
        internal static void SetSessionToken(RequestMessage request, string sessionToken)
        {
            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                request.Headers.Add(HttpConstants.HttpHeaders.SessionToken, sessionToken);
            }
        }

        /// <summary>
        /// Gets or sets the configuration for operation-level metrics.
        /// </summary>
#if PREVIEW
        public
#else
        internal
#endif
        OperationMetricsOptions OperationMetricsOptions { get; set; } = null;

        /// <summary>
        /// Gets or sets the configuration for network-level metrics.
        /// </summary>
#if PREVIEW
        public
#else
        internal
#endif
        NetworkMetricsOptions NetworkMetricsOptions { get; set; } = null;
    }
}
