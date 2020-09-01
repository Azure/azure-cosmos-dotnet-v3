//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    internal sealed class ChangeFeedEstimatorIterator : FeedIterator<RemainingLeaseWork>
    {
        private const char PKRangeIdSeparator = ':';
        private const char SegmentSeparator = '#';
        private const string LSNPropertyName = "_lsn";

        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly Func<string, string, bool, FeedIterator> monitoredContainerFeedCreator;
        private readonly ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions;
        private readonly AsyncLazy<TryCatch<IReadOnlyList<DocumentServiceLease>>> lazyLeaseDocuments;
        private int currentPage;
        private int maxPage;
        private int pageSize;
        private bool hasMoreResults;

        public ChangeFeedEstimatorIterator(
            DocumentServiceLeaseContainer leaseContainer,
            Func<string, string, bool, FeedIterator> monitoredContainerFeedCreator,
            ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions)
        {
            this.monitoredContainerFeedCreator = monitoredContainerFeedCreator ?? throw new ArgumentNullException(nameof(monitoredContainerFeedCreator));
            this.leaseContainer = leaseContainer ?? throw new ArgumentNullException(nameof(leaseContainer));
            this.changeFeedEstimatorRequestOptions = changeFeedEstimatorRequestOptions ?? new ChangeFeedEstimatorRequestOptions();
            this.lazyLeaseDocuments = new AsyncLazy<TryCatch<IReadOnlyList<DocumentServiceLease>>>(valueFactory: (innerCancellationToken) =>
            {
                return this.TryInitializeLeaseDocumentsAsync(innerCancellationToken);
            });
            this.hasMoreResults = true;
        }

        public override bool HasMoreResults => this.hasMoreResults;

        public override async Task<FeedResponse<RemainingLeaseWork>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(requestOptions: null);
            using (diagnostics.GetOverallScope())
            {
                if (!this.lazyLeaseDocuments.ValueInitialized)
                {
                    using (diagnostics.CreateScope("InitializeLeaseDocuments"))
                    {
                        TryCatch<IReadOnlyList<DocumentServiceLease>> tryInitializeContainerRId = await this.lazyLeaseDocuments.GetValueAsync(cancellationToken).ConfigureAwait(false);
                        if (!tryInitializeContainerRId.Succeeded)
                        {
                            if (!(tryInitializeContainerRId.Exception.InnerException is CosmosException cosmosException))
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
                }

                return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }
        }

        private async Task<FeedResponse<RemainingLeaseWork>> ReadNextInternalAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<DocumentServiceLease> leasesForCurrentPage = this.lazyLeaseDocuments.Result.Result.Skip(this.currentPage * this.pageSize).Take(this.pageSize);
            IEnumerable<Task<List<(RemainingLeaseWork, double)>>> tasks = Partitioner.Create(leasesForCurrentPage)
                .GetPartitions(this.pageSize)
                .Select(partition => Task.Run(async () =>
                {
                    List<(RemainingLeaseWork, double)> partialResults = new List<(RemainingLeaseWork, double)>();
                    using (partition)
                    {
                        while (!cancellationToken.IsCancellationRequested && partition.MoveNext())
                        {
                            DocumentServiceLease item = partition.Current;
                            if (item?.CurrentLeaseToken == null) continue;
                            (long estimation, ResponseMessage responseMessage) = await this.GetRemainingWorkAsync(item, cancellationToken).ConfigureAwait(false);

                            // Attach each diagnostics
                            diagnosticsContext.AddDiagnosticsInternal(responseMessage.DiagnosticsContext);
                            partialResults.Add((new RemainingLeaseWork(item.CurrentLeaseToken, estimation, item.Owner), responseMessage.Headers.RequestCharge));
                        }
                    }

                    return partialResults;
                })).ToArray();

            IEnumerable<List<(RemainingLeaseWork, double)>> partitionResults = await Task.WhenAll(tasks);

            IEnumerable<(RemainingLeaseWork, double)> unifiedResults = partitionResults.SelectMany(r => r);

            ReadOnlyCollection<RemainingLeaseWork> estimations = unifiedResults.Select(r => r.Item1).ToList().AsReadOnly();

            double totalRUCost = unifiedResults.Sum(r => r.Item2);

            this.hasMoreResults = ++this.currentPage == this.maxPage;

            return new ChangeFeedEstimatorFeedResponse(diagnosticsContext.Diagnostics, estimations, totalRUCost);
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
            long parsed = 0;
            if (!long.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
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

        private async Task<(long, ResponseMessage)> GetRemainingWorkAsync(
            DocumentServiceLease existingLease,
            CancellationToken cancellationToken)
        {
            // Current lease schema maps Token to PKRangeId
            string partitionKeyRangeId = existingLease.CurrentLeaseToken;
            using FeedIterator iterator = this.monitoredContainerFeedCreator(
                partitionKeyRangeId,
                existingLease.ContinuationToken,
                string.IsNullOrEmpty(existingLease.ContinuationToken));

            try
            {
                ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.NotModified)
                {
                    response.EnsureSuccessStatusCode();
                }

                long parsedLSNFromSessionToken = ChangeFeedEstimatorIterator.TryConvertToNumber(ExtractLsnFromSessionToken(response.Headers[HttpConstants.HttpHeaders.SessionToken]));
                IEnumerable<JObject> items = ChangeFeedEstimatorIterator.GetItemsFromResponse(response);
                long lastQueryLSN = items.Any()
                    ? ChangeFeedEstimatorIterator.TryConvertToNumber(ChangeFeedEstimatorIterator.GetFirstItemLSN(items)) - 1
                    : parsedLSNFromSessionToken;
                if (lastQueryLSN < 0)
                {
                    return (1, response);
                }

                long leaseTokenRemainingWork = parsedLSNFromSessionToken - lastQueryLSN;
                return (leaseTokenRemainingWork < 0 ? 0 : leaseTokenRemainingWork, response);
            }
            catch (Exception clientException)
            {
                Cosmos.Extensions.TraceException(clientException);
                DefaultTrace.TraceWarning("GetEstimateWork > exception: lease token '{0}'", existingLease.CurrentLeaseToken);
                throw;
            }
        }

        private async Task<TryCatch<IReadOnlyList<DocumentServiceLease>>> TryInitializeLeaseDocumentsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<DocumentServiceLease> leases = await this.leaseContainer.GetAllLeasesAsync().ConfigureAwait(false);
                if (leases == null || leases.Count == 0)
                {
                    leases = (IReadOnlyList<DocumentServiceLease>)new List<RemainingLeaseWork>().AsReadOnly();
                }

                return TryCatch<IReadOnlyList<DocumentServiceLease>>.FromResult(leases);
            }
            catch (CosmosException cosmosException)
            {
                return TryCatch<IReadOnlyList<DocumentServiceLease>>.FromException(cosmosException);
            }
        }

        private class ChangeFeedEstimatorFeedResponse : FeedResponse<RemainingLeaseWork>
        {
            private readonly CosmosDiagnostics cosmosDiagnostics;
            private readonly ReadOnlyCollection<RemainingLeaseWork> remainingLeaseWorks;
            private readonly Headers headers;

            public ChangeFeedEstimatorFeedResponse(
                CosmosDiagnostics cosmosDiagnostics,
                ReadOnlyCollection<RemainingLeaseWork> remainingLeaseWorks,
                double ruCost)
            {
                this.cosmosDiagnostics = cosmosDiagnostics ?? throw new ArgumentNullException(nameof(cosmosDiagnostics));
                this.remainingLeaseWorks = remainingLeaseWorks ?? throw new ArgumentNullException(nameof(remainingLeaseWorks));
                this.headers = new Headers();
                this.headers.RequestCharge = ruCost;
            }

            public override string ContinuationToken => throw new NotSupportedException();

            public override int Count => this.remainingLeaseWorks.Count;

            public override Headers Headers => throw new NotImplementedException();

            public override IEnumerable<RemainingLeaseWork> Resource => this.remainingLeaseWorks;

            public override HttpStatusCode StatusCode => HttpStatusCode.OK;

            public override CosmosDiagnostics Diagnostics => this.cosmosDiagnostics;

            public override IEnumerator<RemainingLeaseWork> GetEnumerator() => this.remainingLeaseWorks.GetEnumerator();
        }
    }
}
