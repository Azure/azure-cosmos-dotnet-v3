//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Security.Permissions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json.Linq;

    internal sealed class ChangeFeedEstimatorIterator : FeedIterator<ChangeFeedProcessorState>
    {
        private const string EstimatorDefaultHostName = "Estimator";
        private const char PKRangeIdSeparator = ':';
        private const char SegmentSeparator = '#';
        private const string LSNPropertyName = "_lsn";

        private readonly ContainerInternal monitoredContainer;
        private readonly ContainerInternal leaseContainer;
        private readonly string processorName;
        private readonly Func<DocumentServiceLease, string, bool, FeedIterator> monitoredContainerFeedCreator;
        private readonly ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions;
        private readonly AsyncLazy<TryCatch<IReadOnlyList<DocumentServiceLease>>> lazyLeaseDocuments;
        private DocumentServiceLeaseContainer documentServiceLeaseContainer;
        private int currentPage;
        private int maxPage;
        private int pageSize;
        private bool hasMoreResults;

        public ChangeFeedEstimatorIterator(
            string processorName,
            ContainerInternal monitoredContainer,
            ContainerInternal leaseContainer,
            ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions)
            : this(
                  processorName,
                  monitoredContainer,
                  leaseContainer,
                  changeFeedEstimatorRequestOptions,
                  (DocumentServiceLease lease, string continuationToken, bool startFromBeginning) => ChangeFeedPartitionKeyResultSetIteratorCore.Create(
                          lease: lease,
                          continuationToken: continuationToken,
                          maxItemCount: 1,
                          container: monitoredContainer,
                          startTime: null,
                          startFromBeginning: string.IsNullOrEmpty(continuationToken)))
        {
        }

        /// <summary>
        /// For testing purposes
        /// </summary>
        internal ChangeFeedEstimatorIterator(
            ContainerInternal monitoredContainer,
            ContainerInternal leaseContainer,
            DocumentServiceLeaseContainer documentServiceLeaseContainer,
            Func<DocumentServiceLease, string, bool, FeedIterator> monitoredContainerFeedCreator,
            ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions)
            : this(
                  processorName: string.Empty,
                  monitoredContainer: monitoredContainer,
                  leaseContainer: leaseContainer,
                  changeFeedEstimatorRequestOptions: changeFeedEstimatorRequestOptions,
                  monitoredContainerFeedCreator: monitoredContainerFeedCreator)
        {
            this.documentServiceLeaseContainer = documentServiceLeaseContainer;
        }

        private ChangeFeedEstimatorIterator(
            string processorName,
            ContainerInternal monitoredContainer,
            ContainerInternal leaseContainer,
            ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions,
            Func<DocumentServiceLease, string, bool, FeedIterator> monitoredContainerFeedCreator)
        {
            this.processorName = processorName ?? throw new ArgumentNullException(nameof(processorName));
            this.monitoredContainer = monitoredContainer ?? throw new ArgumentNullException(nameof(monitoredContainer));
            this.leaseContainer = leaseContainer ?? throw new ArgumentNullException(nameof(leaseContainer));
            this.changeFeedEstimatorRequestOptions = changeFeedEstimatorRequestOptions ?? new ChangeFeedEstimatorRequestOptions();
            if (this.changeFeedEstimatorRequestOptions.MaxItemCount.HasValue
                && this.changeFeedEstimatorRequestOptions.MaxItemCount.Value <= 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(this.changeFeedEstimatorRequestOptions.MaxItemCount)} value should be a positive integer.");
            }

            this.lazyLeaseDocuments = new AsyncLazy<TryCatch<IReadOnlyList<DocumentServiceLease>>>(
                valueFactory: (trace, innerCancellationToken) => this.TryInitializeLeaseDocumentsAsync(innerCancellationToken));
            this.hasMoreResults = true;

            this.monitoredContainerFeedCreator = monitoredContainerFeedCreator;
        }

        public override bool HasMoreResults => this.hasMoreResults;

        public override Task<FeedResponse<ChangeFeedProcessorState>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.ReadNextAsync(NoOpTrace.Singleton, cancellationToken);
        }

        public async Task<FeedResponse<ChangeFeedProcessorState>> ReadNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            using (ITrace readNextTrace = trace.StartChild("Read Next Async", TraceComponent.ChangeFeed, TraceLevel.Info))
            {
                if (!this.lazyLeaseDocuments.ValueInitialized)
                {
                    await this.InitializeLeaseStoreAsync(readNextTrace, cancellationToken);
                    TryCatch<IReadOnlyList<DocumentServiceLease>> tryInitializeLeaseDocuments = await this.lazyLeaseDocuments
                            .GetValueAsync(
                                readNextTrace,
                                cancellationToken)
                            .ConfigureAwait(false);
                    if (!tryInitializeLeaseDocuments.Succeeded)
                    {
                        if (!(tryInitializeLeaseDocuments.Exception.InnerException is CosmosException cosmosException))
                        {
                            throw new InvalidOperationException("Failed to convert to CosmosException.");
                        }

                        throw cosmosException;
                    }

                    this.currentPage = 0;
                    if (this.changeFeedEstimatorRequestOptions.MaxItemCount.HasValue)
                    {
                        this.pageSize = this.changeFeedEstimatorRequestOptions.MaxItemCount.Value;
                        this.maxPage = (int)Math.Ceiling((double)this.lazyLeaseDocuments.Result.Result.Count / this.pageSize);
                    }
                    else
                    {
                        // Get all leases in a single request
                        this.pageSize = this.lazyLeaseDocuments.Result.Result.Count;
                        this.maxPage = 1;
                    }
                }

                return await this.ReadNextInternalAsync(readNextTrace, cancellationToken);
            }
        }

        private async Task<FeedResponse<ChangeFeedProcessorState>> ReadNextInternalAsync(
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.lazyLeaseDocuments.Result.Result.Count == 0)
            {
                // Lease store is empty
                this.hasMoreResults = false;
                return new ChangeFeedEstimatorEmptyFeedResponse(trace);
            }

            IEnumerable<DocumentServiceLease> leasesForCurrentPage = this.lazyLeaseDocuments
                .Result
                .Result
                .Skip(this.currentPage * this.pageSize)
                .Take(this.pageSize);
            IEnumerable<Task<(ChangeFeedProcessorState, ResponseMessage)>> tasks = leasesForCurrentPage
                .Select(lease => this.GetRemainingWorkAsync(lease, trace, cancellationToken))
                .ToArray();

            IEnumerable<(ChangeFeedProcessorState, ResponseMessage)> results = await Task.WhenAll(tasks);

            List<ChangeFeedProcessorState> estimations = new List<ChangeFeedProcessorState>();
            double totalRUCost = 0;
            foreach ((ChangeFeedProcessorState, ResponseMessage) result in results)
            {
                using (result.Item2)
                {
                    totalRUCost += result.Item2.Headers.RequestCharge;
                }

                estimations.Add(result.Item1);
            }

            this.hasMoreResults = ++this.currentPage != this.maxPage;

            return new ChangeFeedEstimatorFeedResponse(trace, estimations.AsReadOnly(), totalRUCost);
        }

        /// <summary>
        /// Parses a Session Token and extracts the LSN.
        /// </summary>
        /// <param name="sessionToken">A Session Token</param>
        /// <returns>LSN value</returns>
        internal static string ExtractLsnFromSessionToken(string sessionToken)
        {
            if (string.IsNullOrEmpty(sessionToken))
            {
                return string.Empty;
            }

            string parsedSessionToken = sessionToken.Substring(sessionToken.IndexOf(ChangeFeedEstimatorIterator.PKRangeIdSeparator) + 1);
            string[] segments = parsedSessionToken.Split(ChangeFeedEstimatorIterator.SegmentSeparator);

            if (segments.Length < 2)
            {
                return segments[0];
            }

            // GlobalLsn
            return segments[1];
        }

        private static string GetFirstItemLSN(IEnumerable<JObject> items)
        {
            JObject item = items.FirstOrDefault();
            if (item == null)
            {
                return null;
            }

            if (item.TryGetValue(LSNPropertyName, StringComparison.OrdinalIgnoreCase, out JToken property))
            {
                return property.Value<string>();
            }

            DefaultTrace.TraceWarning("Change Feed response item does not include LSN.");
            return null;
        }

        private static long TryConvertToNumber(string number)
        {
            if (!long.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out long parsed))
            {
                DefaultTrace.TraceWarning("Cannot parse number '{0}'.", number);
                return 0;
            }

            return parsed;
        }

        private static IEnumerable<JObject> GetItemsFromResponse(ResponseMessage response)
        {
            if (response.Content == null)
            {
                return new Collection<JObject>();
            }

            return CosmosFeedResponseSerializer.FromFeedResponseStream<JObject>(
                CosmosContainerExtensions.DefaultJsonSerializer,
                response.Content);
        }

        private async Task<(ChangeFeedProcessorState, ResponseMessage)> GetRemainingWorkAsync(
            DocumentServiceLease existingLease,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (ITrace getRemainingWorkTrace = trace.StartChild($"Get Remaining Work {existingLease.Id}", TraceComponent.ChangeFeed, TraceLevel.Info))
            {
                using FeedIterator iterator = this.monitoredContainerFeedCreator(
                existingLease,
                existingLease.ContinuationToken,
                string.IsNullOrEmpty(existingLease.ContinuationToken));

                try
                {
                    ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
                    if (response.StatusCode != HttpStatusCode.NotModified)
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    long parsedLSNFromSessionToken = ChangeFeedEstimatorIterator.TryConvertToNumber(ExtractLsnFromSessionToken(response.Headers.Session));
                    IEnumerable<JObject> items = ChangeFeedEstimatorIterator.GetItemsFromResponse(response);
                    long lastQueryLSN = items.Any()
                        ? ChangeFeedEstimatorIterator.TryConvertToNumber(ChangeFeedEstimatorIterator.GetFirstItemLSN(items)) - 1
                        : parsedLSNFromSessionToken;
                    if (lastQueryLSN < 0)
                    {
                        return (new ChangeFeedProcessorState(existingLease.CurrentLeaseToken, 1, existingLease.Owner), response);
                    }

                    long leaseTokenRemainingWork = parsedLSNFromSessionToken - lastQueryLSN;
                    long estimation = leaseTokenRemainingWork < 0 ? 0 : leaseTokenRemainingWork;
                    return (new ChangeFeedProcessorState(existingLease.CurrentLeaseToken, estimation, existingLease.Owner), response);
                }
                catch (Exception clientException)
                {
                    Cosmos.Extensions.TraceException(clientException);
                    DefaultTrace.TraceWarning("GetEstimateWork > exception: lease token '{0}'", existingLease.CurrentLeaseToken);
                    throw;
                }
            }
        }

        private async Task InitializeLeaseStoreAsync(ITrace trace, CancellationToken cancellationToken)
        {
            using (trace.StartChild("Initialize Lease Store", TraceComponent.ChangeFeed, TraceLevel.Info))
            {
                if (this.documentServiceLeaseContainer == null)
                {
                    string monitoredContainerAndDatabaseRid = await this.monitoredContainer.GetMonitoredDatabaseAndContainerRidAsync(cancellationToken);
                    string leasePrefix = this.monitoredContainer.GetLeasePrefix(this.processorName, monitoredContainerAndDatabaseRid);
                    DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager = await DocumentServiceLeaseStoreManagerBuilder.InitializeAsync(
                        monitoredContainer: this.monitoredContainer,
                        leaseContainer: this.leaseContainer,
                        leaseContainerPrefix: leasePrefix,
                        instanceName: ChangeFeedEstimatorIterator.EstimatorDefaultHostName);

                    this.documentServiceLeaseContainer = documentServiceLeaseStoreManager.LeaseContainer;
                }
            }
        }

        private async Task<TryCatch<IReadOnlyList<DocumentServiceLease>>> TryInitializeLeaseDocumentsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                IReadOnlyList<DocumentServiceLease> leases = await this.documentServiceLeaseContainer
                    .GetAllLeasesAsync()
                    .ConfigureAwait(false);

                return TryCatch<IReadOnlyList<DocumentServiceLease>>.FromResult(leases);
            }
            catch (CosmosException cosmosException)
            {
                return TryCatch<IReadOnlyList<DocumentServiceLease>>.FromException(cosmosException);
            }
        }

        private sealed class ChangeFeedEstimatorFeedResponse : FeedResponse<ChangeFeedProcessorState>
        {
            private readonly ReadOnlyCollection<ChangeFeedProcessorState> remainingLeaseWorks;
            private readonly Headers headers;

            public ChangeFeedEstimatorFeedResponse(
                ITrace trace,
                ReadOnlyCollection<ChangeFeedProcessorState> remainingLeaseWorks,
                double ruCost)
            {
                this.Trace = trace ?? throw new ArgumentNullException(nameof(trace));
                this.remainingLeaseWorks = remainingLeaseWorks ?? throw new ArgumentNullException(nameof(remainingLeaseWorks));
                this.headers = new Headers
                {
                    RequestCharge = ruCost
                };
            }

            public ITrace Trace { get; }

            public override string ContinuationToken => throw new NotSupportedException();

            public override int Count => this.remainingLeaseWorks.Count;

            public override Headers Headers => this.headers;

            public override IEnumerable<ChangeFeedProcessorState> Resource => this.remainingLeaseWorks;

            public override HttpStatusCode StatusCode => HttpStatusCode.OK;

            public override CosmosDiagnostics Diagnostics => new CosmosTraceDiagnostics(this.Trace);

            public override IEnumerator<ChangeFeedProcessorState> GetEnumerator()
            {
                return this.remainingLeaseWorks.GetEnumerator();
            }
        }

        private sealed class ChangeFeedEstimatorEmptyFeedResponse : FeedResponse<ChangeFeedProcessorState>
        {
            private readonly static IEnumerable<ChangeFeedProcessorState> remainingLeaseWorks = Enumerable.Empty<ChangeFeedProcessorState>();
            private readonly Headers headers;

            public ChangeFeedEstimatorEmptyFeedResponse(ITrace trace)
            {
                this.Trace = trace ?? throw new ArgumentNullException(nameof(trace));
                this.headers = new Headers();
            }

            public ITrace Trace { get; }

            public override string ContinuationToken => throw new NotSupportedException();

            public override int Count => 0;

            public override Headers Headers => throw new NotImplementedException();

            public override IEnumerable<ChangeFeedProcessorState> Resource => ChangeFeedEstimatorEmptyFeedResponse.remainingLeaseWorks;

            public override HttpStatusCode StatusCode => HttpStatusCode.OK;

            public override CosmosDiagnostics Diagnostics => new CosmosTraceDiagnostics(this.Trace);

            public override IEnumerator<ChangeFeedProcessorState> GetEnumerator()
            {
                return ChangeFeedEstimatorEmptyFeedResponse.remainingLeaseWorks.GetEnumerator();
            }
        }
    }
}
