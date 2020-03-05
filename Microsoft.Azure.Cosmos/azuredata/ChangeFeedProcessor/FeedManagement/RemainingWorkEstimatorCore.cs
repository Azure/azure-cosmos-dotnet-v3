//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    internal sealed class RemainingWorkEstimatorCore : RemainingWorkEstimator
    {
        private const char PKRangeIdSeparator = ':';
        private const char SegmentSeparator = '#';
        private static readonly JsonEncodedText LSNPropertyName = JsonEncodedText.Encode("_lsn");
        private static readonly CosmosSerializer DefaultSerializer = CosmosTextJsonSerializer.CreatePropertiesSerializer();
        private readonly Func<string, string, bool, FeedIterator> feedCreator;
        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly int degreeOfParallelism;

        public RemainingWorkEstimatorCore(
            DocumentServiceLeaseContainer leaseContainer,
            Func<string, string, bool, FeedIterator> feedCreator,
            int degreeOfParallelism)
        {
            if (leaseContainer == null)
            {
                throw new ArgumentNullException(nameof(leaseContainer));
            }

            if (feedCreator == null)
            {
                throw new ArgumentNullException(nameof(feedCreator));
            }

            if (degreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException("Degree of parallelism is out of range", nameof(degreeOfParallelism));
            }

            this.leaseContainer = leaseContainer;
            this.feedCreator = feedCreator;
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
                                Microsoft.Azure.Cosmos.Extensions.TraceException(ex);
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

        private static long GetFirstItemLSN(Collection<JsonElement> items)
        {
            JsonElement? item = RemainingWorkEstimatorCore.GetFirstItem(items);
            if (!item.HasValue)
            {
                return 0;
            }

            if (item.Value.TryGetProperty(LSNPropertyName.EncodedUtf8Bytes, out JsonElement property))
            {
                return property.GetInt64();
            }

            DefaultTrace.TraceWarning("Change Feed response item does not include LSN.");
            return 0;
        }

        private static JsonElement? GetFirstItem(Collection<JsonElement> response)
        {
            using (IEnumerator<JsonElement> e = response.GetEnumerator())
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
                DefaultTrace.TraceWarning("Cannot parse number '{0}'.", number);
                return 0;
            }

            return parsed;
        }

        private static Collection<JsonElement> GetItemsFromResponse(Response response)
        {
            if (response.ContentStream == null)
            {
                return new Collection<JsonElement>();
            }

            return RemainingWorkEstimatorCore.DefaultSerializer.FromStream<CosmosFeedResponseUtil<JsonElement>>(response.ContentStream).Data;
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
                Response response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
                if (response.Status != (int)HttpStatusCode.NotModified)
                {
                    response.EnsureSuccessStatusCode();
                }

                response.Headers.TryGetValue(HttpConstants.HttpHeaders.SessionToken, out string sessionToken);
                long parsedLSNFromSessionToken = RemainingWorkEstimatorCore.TryConvertToNumber(ExtractLsnFromSessionToken(sessionToken));
                Collection<JsonElement> items = RemainingWorkEstimatorCore.GetItemsFromResponse(response);
                long lastQueryLSN = items.Count > 0
                    ? RemainingWorkEstimatorCore.GetFirstItemLSN(items) - 1
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
                Microsoft.Azure.Cosmos.Extensions.TraceException(clientException);
                DefaultTrace.TraceWarning("GetEstimateWork > exception: lease token '{0}'", existingLease.CurrentLeaseToken);
                throw;
            }
        }
    }
}
