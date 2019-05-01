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
    public abstract class CosmosFeedResponse<T> : IEnumerable<T>
    {
        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public abstract double RequestCharge { get; }

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId { get; }

        /// <summary>
        /// Gets the continuation token to be used for continuing enumeration of the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The continuation token to be used for continuing enumeration.
        /// </value>
        public abstract string Continuation { get; }

        /// <summary>
        /// Gets the session token for use in session consistency reads from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The session token for use in session consistency.
        /// </value>
        public abstract string SessionToken { get; }

        /// <summary>
        /// The headers of the response
        /// </summary>
        public abstract CosmosResponseMessageHeaders Headers { get; }

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