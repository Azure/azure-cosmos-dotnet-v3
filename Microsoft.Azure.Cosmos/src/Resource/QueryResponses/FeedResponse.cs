//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The user contract for the various feed responses that serialized the responses to a type.
    /// To follow the .NET standard for typed responses any exceptions should be thrown to the user.
    /// </summary>
    public abstract class FeedResponse<T> : Response<IEnumerable<T>>, IEnumerable<T>
    {
        /// <summary>
        /// Create an empty cosmos feed response for mock testing
        /// </summary>
        protected FeedResponse()
        {
        }

        /// <summary>
        /// Create a FeedResponse object with the default properties set
        /// </summary>
        /// <param name="httpStatusCode">The status code of the response</param>
        /// <param name="headers">The headers of the response</param>
        /// <param name="resource">The object from the response</param>
        internal FeedResponse(
            HttpStatusCode httpStatusCode,
            Headers headers,
            IEnumerable<T> resource)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = resource;
        }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override IEnumerable<T> Resource { get; }

        /// <inheritdoc/>
        public override HttpStatusCode StatusCode { get; }

        /// <inheritdoc/>
        public override double RequestCharge => this.Headers?.RequestCharge ?? 0;

        /// <inheritdoc/>
        public override string ActivityId => this.Headers?.ActivityId;

        /// <inheritdoc/>
        public override string ETag => this.Headers?.ETag;

        /// <inheritdoc/>
        internal override string MaxResourceQuota => this.Headers?.GetHeaderValue<string>(HttpConstants.HttpHeaders.MaxResourceQuota);

        /// <inheritdoc/>
        internal override string CurrentResourceQuotaUsage => this.Headers?.GetHeaderValue<string>(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage);

        /// <summary>
        /// Gets the continuation token to be used for continuing enumeration of the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The continuation token to be used for continuing enumeration.
        /// </value>
        public abstract string Continuation { get; }

        /// <summary>
        /// The number of items in the stream.
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// Get an enumerator of the object
        /// </summary>
        /// <returns>An instance of an Enumerator</returns>
        public abstract IEnumerator<T> GetEnumerator();

        /// <summary>
        /// Get an enumerator of the object
        /// </summary>
        /// <returns>An instance of an Enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal abstract string InternalContinuationToken { get; }

        internal abstract bool HasMoreResults { get; }
    }
}