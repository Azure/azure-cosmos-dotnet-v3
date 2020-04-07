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
    /// ChangeFeedIterator<MyClass> feedIterator = this.Container.GetChangeFeedIterator<MyClass>();
    /// while (feedIterator.HasMoreResults)
    /// {
    ///     FeedResponse<MyClass> response = await feedIterator.ReadNextAsync();
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
    abstract class ChangeFeedIterator<T> : FeedIterator<T>
    {
        /// <summary>
        /// Continuation that represents the last point read from the change feed.
        /// </summary>
        public abstract string Continuation { get; }
    }
}
