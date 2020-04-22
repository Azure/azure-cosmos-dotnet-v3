//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
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
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    internal sealed class RemainingWorkEstimatorCore : RemainingWorkEstimator
    {
        private const char PKRangeIdSeparator = ':';
        private const char SegmentSeparator = '#';
        private const string LSNPropertyName = "_lsn";
        private readonly Func<string, string, bool, FeedIterator> feedCreator;
        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly int degreeOfParallelism;

        public RemainingWorkEstimatorCore(
            DocumentServiceLeaseContainer leaseContainer,
            Func<string, string, bool, FeedIterator> feedCreator,
            int degreeOfParallelism)
        {
            if (degreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException("Degree of parallelism is out of range", nameof(degreeOfParallelism));
            }

            this.leaseContainer = leaseContainer ?? throw new ArgumentNullException(nameof(leaseContainer));
            this.feedCreator = feedCreator ?? throw new ArgumentNullException(nameof(feedCreator));
            this.degreeOfParallelism = degreeOfParallelism;
        }

        public override async Task<long> GetEstimatedRemainingWorkAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<RemainingLeaseTokenWork> leaseTokens = await this.GetEstimatedRemainingWorkPerLeaseTokenAsync(cancellationToken);
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
                            catch (CosmosException ex)
                            {
                                Cosmos.Extensions.TraceException(ex);
                                DefaultTrace.TraceWarning("Getting estimated work for lease token {0} failed!", item.CurrentLeaseToken);
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
        /// <returns>LSN value</returns>
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

        private static JObject GetFirstItem(Collection<JObject> response)
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

            return CosmosContainerExtensions.DefaultJsonSerializer.FromFeedResponseStream<JObject>(
                response.Content,
                ResourceType.Document);
        }

        private async Task<long> GetRemainingWorkAsync(DocumentServiceLease existingLease, CancellationToken cancellationToken)
        {
            // Current lease schema maps Token to PKRangeId
            string partitionKeyRangeId = existingLease.CurrentLeaseToken;
            FeedIterator iterator = this.feedCreator(
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

                long parsedLSNFromSessionToken = RemainingWorkEstimatorCore.TryConvertToNumber(ExtractLsnFromSessionToken(response.Headers[HttpConstants.HttpHeaders.SessionToken]));
                IEnumerable<JObject> items = RemainingWorkEstimatorCore.GetItemsFromResponse(response);
                long lastQueryLSN = items.Any()
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
                Cosmos.Extensions.TraceException(clientException);
                DefaultTrace.TraceWarning("GetEstimateWork > exception: lease token '{0}'", existingLease.CurrentLeaseToken);
                throw;
            }
        }
    }
}
