//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Scripts;

    /// <summary>
    /// This response factory converts response messages
    /// to the corresponding type response using the
    /// CosmosClient serializer
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    abstract class CosmosResponseFactory
    {
        /// <summary>
        /// Creates a FeedResponse from a response message
        /// </summary>
        /// <typeparam name="T">The user type of the serialized item</typeparam>
        /// <param name="responseMessage">The response message from the stream API</param>
        /// <returns>An instance of FeedResponse<typeparamref name="T"/></returns>
        public abstract FeedResponse<T> CreateItemFeedResponse<T>(
            ResponseMessage responseMessage);

        /// <summary>
        /// Creates a FeedResponse from a response message
        /// </summary>
        /// <typeparam name="T">The user</typeparam>
        /// <param name="responseMessage">The response message from the stream API</param>
        /// <returns>An instance of FeedResponse<typeparamref name="T"/></returns>
        public abstract ItemResponse<T> CreateItemResponse<T>(
            ResponseMessage responseMessage);

        /// <summary>
        /// Creates a StoredProcedureExecuteResponse from a response message
        /// </summary>
        /// <typeparam name="T">The user</typeparam>
        /// <param name="responseMessage">The response message from the stream API</param>
        /// <returns>An instance of FeedResponse<typeparamref name="T"/></returns>
        public abstract StoredProcedureExecuteResponse<T> CreateStoredProcedureExecuteResponse<T>(
            ResponseMessage responseMessage);
    }
}