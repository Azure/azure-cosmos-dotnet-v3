//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Cosmos Result set iterator that keeps track of the continuation token when retrieving results from the change feed.
    /// </summary>
    /// <example>
    /// <code language="c#">
    /// <![CDATA[
    /// ChangeFeedIterator feedIterator = this.Container.GetChangeFeedStreamIterator();
    /// while (feedIterator.HasMoreResults)
    /// {
    ///     // Stream iterator returns a response with status code
    ///     using(ResponseMessage response = await feedIterator.ReadNextAsync())
    ///     {
    ///         // Handle failure scenario
    ///         if(!response.IsSuccessStatusCode)
    ///         {
    ///             // Log the response.Diagnostics and handle the error
    ///         }
    ///     }
    /// }
    ///
    /// string previousContinuation = feedIterator.Continuation;
    /// ]]>
    /// </code>
    /// </example>
#if PREVIEW
    public
#else
    internal
#endif
    abstract class ChangeFeedIterator : FeedIterator
    {
        /// <summary>
        /// Continuation that represents the last point read from the change feed.
        /// </summary>
        public abstract string Continuation { get; }
    }
}
