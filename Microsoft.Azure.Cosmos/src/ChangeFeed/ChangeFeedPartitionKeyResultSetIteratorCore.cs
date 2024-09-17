//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos Change Feed Iterator for a particular Partition Key Range
    /// </summary>
    internal sealed class ChangeFeedPartitionKeyResultSetIteratorCore : FeedIteratorInternal
    {
        public static ChangeFeedPartitionKeyResultSetIteratorCore Create(
            DocumentServiceLease lease,
            ChangeFeedMode mode,
            string continuationToken,
            int? maxItemCount,
            ContainerInternal container,
            DateTime? startTime,
            bool startFromBeginning)
        {
            // If the lease represents a full partition (old schema) then use a FeedRangePartitionKeyRange
            // If the lease represents an EPK range (new schema) the use the FeedRange in the lease
            FeedRangeInternal feedRange = lease is DocumentServiceLeaseCoreEpk ? lease.FeedRange : new FeedRangePartitionKeyRange(lease.CurrentLeaseToken);

            ChangeFeedStartFrom startFrom;
            if (continuationToken != null)
            {
                // For continuation based feed range we need to manufactor a new continuation token that has the partition key range id in it.
                startFrom = new ChangeFeedStartFromContinuationAndFeedRange(continuationToken, feedRange);
            }
            else if (startTime.HasValue)
            {
                startFrom = ChangeFeedStartFrom.Time(startTime.Value, feedRange);
            }
            else if (startFromBeginning)
            {
                startFrom = ChangeFeedStartFrom.Beginning(feedRange);
            }
            else
            {
                startFrom = ChangeFeedStartFrom.Now(feedRange);
            }

            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions()
            {
                PageSizeHint = maxItemCount,
            };

            return new ChangeFeedPartitionKeyResultSetIteratorCore(
                container: container,
                mode: mode,
                changeFeedStartFrom: startFrom,
                options: requestOptions);
        }

        private readonly CosmosClientContext clientContext;

        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private readonly ChangeFeedMode mode;

        private ChangeFeedStartFrom changeFeedStartFrom;
        private bool hasMoreResultsInternal;

        private ChangeFeedPartitionKeyResultSetIteratorCore(
            ContainerInternal container,
            ChangeFeedMode mode,
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedRequestOptions options)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.mode = mode;
            this.changeFeedStartFrom = changeFeedStartFrom ?? throw new ArgumentNullException(nameof(changeFeedStartFrom));
            this.clientContext = this.container.ClientContext;
            this.changeFeedOptions = options;

            this.operationName = OpenTelemetryConstants.Operations.QueryChangeFeed;
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A change feed response from cosmos service</returns>
        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.clientContext.OperationHelperAsync(
                                operationName: "Change Feed Processor Read Next Async",
                                containerName: this.container?.Id,
                                databaseName: this.container?.Database?.Id ?? this.databaseName,
                                operationType: Documents.OperationType.ReadFeed,
                                requestOptions: this.changeFeedOptions,
                                task: (trace) => this.ReadNextAsync(trace, cancellationToken),
                                traceComponent: TraceComponent.ChangeFeed,
                                traceLevel: TraceLevel.Info);
        }

        public override async Task<ResponseMessage> ReadNextAsync(ITrace trace, CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                cosmosContainerCore: this.container,
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: this.changeFeedOptions,
                requestEnricher: (requestMessage) =>
                {
                    // Set time headers if any
                    ChangeFeedStartFromRequestOptionPopulator visitor = new ChangeFeedStartFromRequestOptionPopulator(requestMessage);
                    this.changeFeedStartFrom.Accept(visitor);

                    if (this.changeFeedOptions.PageSizeHint.HasValue)
                    {
                        requestMessage.Headers.Add(
                            HttpConstants.HttpHeaders.PageSize,
                            this.changeFeedOptions.PageSizeHint.Value.ToString(CultureInfo.InvariantCulture));
                    }

                    // Add the necessary mode headers
                    this.mode.Accept(requestMessage);
                },
                feedRange: this.changeFeedStartFrom.FeedRange,
                streamPayload: default,
                trace: trace,
                cancellationToken: cancellationToken);

            // Change Feed uses etag as continuation token.
            string etag = responseMessage.Headers.ETag;
            this.hasMoreResultsInternal = responseMessage.IsSuccessStatusCode;
            responseMessage.Headers.ContinuationToken = etag;
            this.changeFeedStartFrom = new ChangeFeedStartFromContinuationAndFeedRange(etag, (FeedRangeInternal)this.changeFeedStartFrom.FeedRange);

            return responseMessage;
        }
    }
}
