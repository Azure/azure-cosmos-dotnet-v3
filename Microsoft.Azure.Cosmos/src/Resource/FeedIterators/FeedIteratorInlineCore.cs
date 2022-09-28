//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class FeedIteratorInlineCore : FeedIteratorInternal
    {
        private readonly FeedIteratorInternal feedIteratorInternal;
        private readonly CosmosClientContext clientContext;

        internal FeedIteratorInlineCore(
            FeedIterator feedIterator,
            CosmosClientContext clientContext)
        {
            if (!(feedIterator is FeedIteratorInternal feedIteratorInternal))
            {
                throw new ArgumentNullException(nameof(feedIterator));
            }

            this.feedIteratorInternal = feedIteratorInternal;
            this.clientContext = clientContext;

            this.container = feedIteratorInternal.container;
            this.databaseId = feedIteratorInternal.databaseId;
        }

        internal FeedIteratorInlineCore(
            FeedIteratorInternal feedIteratorInternal,
            CosmosClientContext clientContext)
        {
            this.feedIteratorInternal = feedIteratorInternal ?? throw new ArgumentNullException(nameof(feedIteratorInternal));
            this.clientContext = clientContext;

            this.container = feedIteratorInternal.container;
            this.databaseId = feedIteratorInternal.databaseId;
        }

        public override bool HasMoreResults => this.feedIteratorInternal.HasMoreResults;

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            return this.feedIteratorInternal.GetCosmosElementContinuationToken();
        }

        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.clientContext.OperationHelperAsync("FeedIterator Read Next Async",
                        requestOptions: null,
                        task: (trace) => this.feedIteratorInternal.ReadNextAsync(trace, cancellationToken),
                        openTelemetry: (response) =>
                        {
                            if (this.container == null)
                            {
                                return new OpenTelemetryResponse(
                                    responseMessage: response, 
                                    containerName: null,
                                    databaseName: this.databaseId);
                            }
                            return new OpenTelemetryResponse(responseMessage: response,
                                    containerName: this.container?.Id,
                                    databaseName: this.container?.Database?.Id);
                        });
        }

        public override Task<ResponseMessage> ReadNextAsync(ITrace trace, CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => this.feedIteratorInternal.ReadNextAsync(trace, cancellationToken));
        }

        protected override void Dispose(bool disposing)
        {
            this.feedIteratorInternal.Dispose();
            base.Dispose(disposing);
        }
    }
}
