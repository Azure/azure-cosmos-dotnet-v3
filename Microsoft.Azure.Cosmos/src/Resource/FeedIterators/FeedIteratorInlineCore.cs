//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
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

            this.SetupInfoForTelemetry(feedIteratorInternal);
        }

        internal FeedIteratorInlineCore(
            FeedIteratorInternal feedIteratorInternal,
            CosmosClientContext clientContext)
        {
            this.feedIteratorInternal = feedIteratorInternal ?? throw new ArgumentNullException(nameof(feedIteratorInternal));
            this.clientContext = clientContext;

            this.container = feedIteratorInternal.container;

            this.SetupInfoForTelemetry(feedIteratorInternal);
        }

        public override bool HasMoreResults => this.feedIteratorInternal.HasMoreResults;

        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.clientContext.OperationHelperAsync(
                        operationName: "FeedIterator ReadNextAsync",
                        containerName: this.container?.Id,
                        databaseName: this.container?.Database?.Id ?? this.databaseName,
                        operationType: Documents.OperationType.ReadFeed,
                        requestOptions: new RequestOptions()
                        {
                            OperationMetricsOptions = this.operationMetricsOptions,
                            NetworkMetricsOptions = this.networkMetricsOptions,
                        },
                        task: (trace) => this.feedIteratorInternal.ReadNextAsync(trace, cancellationToken),
                        openTelemetry: new (this.operationName, (response) =>
                        {
                            OpenTelemetryResponse openTelemetryResponse = new OpenTelemetryResponse(responseMessage: response, querySpecFunc: () => this.querySpec);

                            if (this.operationType.HasValue)
                            {
                                openTelemetryResponse.OperationType = this.operationType.Value;
                            }
                            return openTelemetryResponse;
                        }));
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
