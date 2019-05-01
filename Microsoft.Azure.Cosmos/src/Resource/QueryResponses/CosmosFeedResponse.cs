//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;

    /// <summary>
    /// The user contract for the various feed responses that serialized the responses to a type.
    /// To follow the .NET standard for typed responses any exceptions should be thrown to the user.
    /// </summary>
    public abstract class CosmosFeedResponse<T> : CosmosResponse<IEnumerable<T>>, IEnumerable<T>
    {
        /// <summary>
        /// Create an empty cosmos feed response for mock testing
        /// </summary>
        public CosmosFeedResponse()
        {

        }

        /// <summary>
        /// Create a CosmosFeedResponse object with the default properties set
        /// </summary>
        /// <param name="httpStatusCode">The status code of the response</param>
        /// <param name="headers">The headers of the response</param>
        /// <param name="resource">The object from the response</param>
        internal CosmosFeedResponse(
            HttpStatusCode httpStatusCode,
            CosmosResponseMessageHeaders headers,
            IEnumerable<T> resource):base(
                httpStatusCode,
                headers,
                resource)
        {
        }

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
        /// <returns></returns>
        public abstract IEnumerator<T> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal abstract string InternalContinuationToken { get; }

        internal abstract bool HasMoreResults { get; }
    }
}