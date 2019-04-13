//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Represents the template class used by feed methods (enumeration operations) for the Azure Cosmos DB service.
    /// </summary>
    /// <typeparam name="T">The feed type.</typeparam>
    internal class CosmosElementResponse : IEnumerable<CosmosElement>
    {
        private readonly IEnumerable<CosmosElement> inner;
        internal readonly string disallowContinuationTokenMessage;

        /// <summary>
        /// Constructor exposed for mocking purposes.
        /// </summary>
        public CosmosElementResponse()
        {
        }

        internal CosmosElementResponse(
            IEnumerable<CosmosElement> result,
            int count,
            CosmosResponseMessageHeaders responseHeaders,
            bool useETagAsContinuation = false,
            string disallowContinuationTokenMessage = null,
            long responseLengthBytes = 0)
        {
            this.Count = count;
            this.UseETagAsContinuation = useETagAsContinuation;
            this.disallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.ResponseLengthBytes = responseLengthBytes;
        }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in reqest units.
        /// </value>
        public double RequestCharge => this.Headers.RequestCharge;
        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public string ActivityId => this.Headers[HttpConstants.HttpHeaders.ActivityId];

        /// <summary>
        /// Gets the continuation token to be used for continuing enumeration of the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The continuation token to be used for continuing enumeration.
        /// </value>
        public string ResponseContinuation
        {
            get
            {
                if (this.disallowContinuationTokenMessage != null)
                {
                    throw new ArgumentException(this.disallowContinuationTokenMessage);
                }

                return this.InternalResponseContinuation;
            }

            internal set
            {
                if (this.disallowContinuationTokenMessage != null)
                {
                    throw new ArgumentException(this.disallowContinuationTokenMessage);
                }

                Debug.Assert(!this.UseETagAsContinuation);
                this.Headers.Continuation = value;
            }
        }

        /// <summary>
        /// Gets the session token for use in sesssion consistency reads from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The session token for use in session consistency.
        /// </value>
        public string SessionToken => this.Headers[HttpConstants.HttpHeaders.SessionToken];

        /// <summary>
        /// Gets the content parent location, for example, dbs/foo/colls/bar, from the Azure Cosmos DB service.
        /// </summary>
        public string ContentLocation => this.Headers[HttpConstants.HttpHeaders.OwnerFullName];

        /// <summary>
        /// Gets the entity tag associated with last transaction in the Azure Cosmos DB service,
        /// which can be used as If-Non-Match Access condition for ReadFeed REST request or 
        /// ContinuationToken property of <see cref="ChangeFeedOptions"/> parameter for
        /// <see cref="DocumentClient.CreateDocumentChangeFeedQuery(string, ChangeFeedOptions)"/> 
        /// to get feed changes since the transaction specified by this entity tag.
        /// </summary>
        public string ETag => this.Headers.ETag;

        public CosmosResponseMessageHeaders Headers { get; }

        public int Count { get; }

        /// <summary>
        /// Gets the response length in bytes
        /// </summary>
        /// <remarks>
        /// This value is only set for Direct mode.
        /// </remarks>
        internal long ResponseLengthBytes { get; private set; }

        /// <summary>
        /// Get the client side request statistics for the current request.
        /// </summary>
        /// <remarks>
        /// This value is currently used for tracking replica Uris.
        /// </remarks>
        internal ClientSideRequestStatistics RequestStatistics { get; private set; }

        /// <summary>
        /// Gets the continuation token to be used for continuing enumeration of the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The continuation token to be used for continuing enumeration.
        /// </value>
        internal string InternalResponseContinuation => this.UseETagAsContinuation ?
                    this.ETag :
                    this.Headers.Continuation;

        // This is used by CosmosElementResponseBinder.
        internal bool UseETagAsContinuation { get; }

        internal string DisallowContinuationTokenMessage => this.disallowContinuationTokenMessage;

        
        /// <summary>
        /// Returns an enumerator that iterates through a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <returns>An IEnumerator object that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<CosmosElement> GetEnumerator()
        {
            return this.inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.inner.GetEnumerator();
        }
    }
}
