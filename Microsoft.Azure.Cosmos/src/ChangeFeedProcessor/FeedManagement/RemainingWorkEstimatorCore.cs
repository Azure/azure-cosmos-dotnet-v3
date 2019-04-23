//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Logging;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    internal sealed class RemainingWorkEstimatorCore : RemainingWorkEstimator
    {
        private const char PKRangeIdSeparator = ':';
        private const char SegmentSeparator = '#';
        private const string LSNPropertyName = "_lsn";
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private static readonly CosmosJsonSerializer DefaultSerializer = new CosmosDefaultJsonSerializer();
        private readonly CosmosContainer container;
        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly int degreeOfParallelism;

        public RemainingWorkEstimatorCore(
            DocumentServiceLeaseContainer leaseContainer,
            CosmosContainer container,
            int degreeOfParallelism)
        {
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (degreeOfParallelism < 1) throw new ArgumentException("Degree of parallelism is out of range", nameof(degreeOfParallelism));

            this.leaseContainer = leaseContainer;
            this.container = container;
            this.degreeOfParallelism = degreeOfParallelism;
        }

        public override async Task<long> GetEstimatedRemainingWorkAsync(CancellationToken cancellationToken)
        {
            var leaseTokens = await this.GetEstimatedRemainingWorkPerLeaseTokenAsync(cancellationToken);
            if (leaseTokens.Count == 0) return 1;

            return leaseTokens.Sum(leaseToken => leaseToken.RemainingWork);
        }

        public override async Task<IReadOnlyList<RemainingLeaseTokenWork>> GetEstimatedRemainingWorkPerLeaseTokenAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<DocumentServiceLease> leases = await this.leaseContainer.GetAllLeasesAsync().ConfigureAwait(false);
            if (leases == null || leases.Count == 0)
            {
                return new List<RemainingLeaseTokenWork>().AsReadOnly();
            }

            IEnumerable<Task<List<RemainingLeaseTokenWork>>> tasks = Partitioner.Create(leases)
                .GetPartitions(this.degreeOfParallelism)
                .Select(partition => Task.Run(async () =>
                {
                    List<RemainingLeaseTokenWork> partialResults = new List<RemainingLeaseTokenWork>();
                    using (partition)
                    {
                        while (!cancellationToken.IsCancellationRequested && partition.MoveNext())
                        {
                            DocumentServiceLease item = partition.Current;
                            try
                            {
                                if (string.IsNullOrEmpty(item?.CurrentLeaseToken)) continue;
                                long result = await this.GetRemainingWorkAsync(item, cancellationToken);
                                partialResults.Add(new RemainingLeaseTokenWork(item.CurrentLeaseToken, result));
                            }
                            catch (DocumentClientException ex)
                            {
                                Logger.WarnException($"Getting estimated work for lease token {item.CurrentLeaseToken} failed!", ex);
                            }
                        }
                    }

                    return partialResults;
                })).ToArray();

            IEnumerable<List<RemainingLeaseTokenWork>> results = await Task.WhenAll(tasks);
            return results.SelectMany(r => r).ToList().AsReadOnly();
        }

        /// <summary>
        /// Parses a Session Token and extracts the LSN.
        /// </summary>
        /// <param name="sessionToken">A Session Token</param>
        /// <returns>Lsn value</returns>
        internal static string ExtractLsnFromSessionToken(string sessionToken)
        {
            if (string.IsNullOrEmpty(sessionToken))
            {
                return string.Empty;
            }

            string parsedSessionToken = sessionToken.Substring(sessionToken.IndexOf(RemainingWorkEstimatorCore.PKRangeIdSeparator) + 1);
            string[] segments = parsedSessionToken.Split(RemainingWorkEstimatorCore.SegmentSeparator);

            if (segments.Length < 2)
            {
                return segments[0];
            }

            // GlobalLsn
            return segments[1];
        }

        private async Task<long> GetRemainingWorkAsync(DocumentServiceLease existingLease, CancellationToken cancellationToken)
        {
            // Current lease schema maps Token to PKRangeId
            string partitionKeyRangeId = existingLease.CurrentLeaseToken;
            CosmosChangeFeedPartitionKeyResultSetIteratorCore iterator = ResultSetIteratorUtils.BuildResultSetIterator(
                partitionKeyRangeId: partitionKeyRangeId,
                continuationToken: existingLease.ContinuationToken,
                maxItemCount: 1,
                cosmosContainer: (CosmosContainerCore)this.container,
                startTime: null,
                startFromBeginning: string.IsNullOrEmpty(existingLease.ContinuationToken));

            try
            {
                CosmosResponseMessage response = await iterator.FetchNextSetAsync(cancellationToken).ConfigureAwait(false);
                if (response.StatusCode != System.Net.HttpStatusCode.NotModified)
                {
                    response.EnsureSuccessStatusCode();
                }

                long parsedLSNFromSessionToken = RemainingWorkEstimatorCore.TryConvertToNumber(ExtractLsnFromSessionToken(response.Headers[HttpConstants.HttpHeaders.SessionToken]));
                System.Collections.ObjectModel.Collection<JObject> items = RemainingWorkEstimatorCore.GetItemsFromResponse(response);
                long lastQueryLSN = items.Count > 0
                    ? RemainingWorkEstimatorCore.TryConvertToNumber(RemainingWorkEstimatorCore.GetFirstItemLSN(items)) - 1
                    : parsedLSNFromSessionToken;
                if (lastQueryLSN < 0)
                {
                    return 1;
                }

                long leaseTokenRemainingWork = parsedLSNFromSessionToken - lastQueryLSN;
                return leaseTokenRemainingWork < 0 ? 0 : leaseTokenRemainingWork;
            }
            catch (Exception clientException)
            {
                Logger.WarnException($"GetEstimateWork > exception: lease token '{existingLease.CurrentLeaseToken}'", clientException);
                throw;
            }
        }

        private static string GetFirstItemLSN(System.Collections.ObjectModel.Collection<JObject> items)
        {
            JObject item = RemainingWorkEstimatorCore.GetFirstItem(items);
            if (item == null)
            {
                return null;
            }



            if (item.TryGetValue(LSNPropertyName, StringComparison.OrdinalIgnoreCase, out JToken property))
            {
                return property.Value<string>();
            }

            throw new InvalidOperationException("Change Feed response item does not include LSN.");
        }

        private static JObject GetFirstItem(System.Collections.ObjectModel.Collection<JObject> response)
        {
            using (IEnumerator<JObject> e = response.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    return e.Current;
                }
            }

            return null;
        }

        private static long TryConvertToNumber(string number)
        {
            long parsed = 0;
            if (!long.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                Logger.WarnFormat(string.Format(CultureInfo.InvariantCulture, "Cannot parse number '{0}'.", number));
                return 0;
            }

            return parsed;
        }

        private static System.Collections.ObjectModel.Collection<JObject> GetItemsFromResponse(CosmosResponseMessage response)
        {
            if (response.Content == null)
            {
                return new System.Collections.ObjectModel.Collection<JObject>();
            }

            return RemainingWorkEstimatorCore.DefaultSerializer.FromStream<CosmosFeedResponse<JObject>>(response.Content).Data;
        }
    }
}
