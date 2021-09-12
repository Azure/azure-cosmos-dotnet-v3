//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Internal API for FeedIterator<typeparamref name="T"/> for inheritance and mocking purposes.
    /// </summary>
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class FeedIteratorInternal<T> : FeedIterator<T>
    {
        public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.ReadNextWithRootTraceAsync(cancellationToken));
        }

        private async Task<FeedResponse<T>> ReadNextWithRootTraceAsync(CancellationToken cancellationToken = default)
        {
            using (ITrace trace = Trace.GetRootTrace("Typed FeedIterator ReadNextAsync", TraceComponent.Unknown, TraceLevel.Info))
            {
                return await this.ReadNextAsync(trace, cancellationToken);
            }
        }

        public abstract Task<FeedResponse<T>> ReadNextAsync(ITrace trace, CancellationToken cancellationToken);

        public abstract CosmosElement GetCosmosElementContinuationToken();
    }
}