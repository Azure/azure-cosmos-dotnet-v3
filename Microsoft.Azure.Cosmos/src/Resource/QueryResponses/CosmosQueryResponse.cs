//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// The cosmos query response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CosmosQueryResponse<T> : IEnumerable<T>
    {
        /// <summary>
        /// Contains the metadata from the response headers from the Azure Cosmos DB call
        /// </summary>
        public IEnumerable<T> Resources;

        private bool HasMoreResults;

        /// <summary>
        /// Create a <see cref="CosmosQueryResponse{T}"/>
        /// </summary>
        protected CosmosQueryResponse()
        {
        }

        /// <summary>
        /// Gets the continuation token
        /// </summary>
        public virtual string ContinuationToken { get; protected set; }

        /// <summary>
        /// Get the enumerators to iterate through the results
        /// </summary>
        /// <returns>An enumerator of the response objects</returns>
        public virtual IEnumerator<T> GetEnumerator()
        {
            if (this.Resources == null)
            {
                return Enumerable.Empty<T>().GetEnumerator();
            }

            return this.Resources.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            Stream stream,
            CosmosJsonSerializer jsonSerializer,
            string continuationToken,
            bool hasMoreResults)
        {
            using (stream)
            {
                CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>()
                {
                    ContinuationToken = continuationToken,
                    HasMoreResults = hasMoreResults
                };

                queryResponse.InitializeResource(stream, jsonSerializer);
                return queryResponse;
            }
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            IEnumerable<TInput> resources,
            string continuationToken,
            bool hasMoreResults)
        {
            CosmosQueryResponse<TInput> queryResponse = new CosmosQueryResponse<TInput>();
            queryResponse.SetProperties(resources, continuationToken, hasMoreResults);
            return queryResponse;
        }

        private void InitializeResource(
            Stream stream,
            CosmosJsonSerializer jsonSerializer)
        {
            this.Resources = jsonSerializer.FromStream<CosmosFeedResponse<T>>(stream).Data;
        }

        private void SetProperties(
            IEnumerable<T> resources,
            string continuationToken,
            bool hasMoreResults)
        {
            this.ContinuationToken = continuationToken;
            this.Resources = resources;
            this.HasMoreResults = hasMoreResults;
        }

        internal bool GetHasMoreResults()
        {
            return this.HasMoreResults;
        }
    }
}