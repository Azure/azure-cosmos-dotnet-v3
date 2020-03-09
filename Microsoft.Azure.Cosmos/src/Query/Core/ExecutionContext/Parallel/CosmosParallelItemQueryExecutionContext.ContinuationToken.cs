//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Newtonsoft.Json;

    /// <summary>
    /// CosmosParallelItemQueryExecutionContext is a concrete implementation for CrossPartitionQueryExecutionContext.
    /// This class is responsible for draining cross partition queries that do not have order by conditions.
    /// The way parallel queries work is that it drains from the left most partition first.
    /// This class handles draining in the correct order and can also stop and resume the query 
    /// by generating a continuation token and resuming from said continuation token.
    /// </summary>
    internal sealed partial class CosmosParallelItemQueryExecutionContext : CosmosCrossPartitionQueryExecutionContext
    {
        /// <summary>
        /// For parallel queries the continuation token semantically holds two pieces of information:
        /// 1) What physical partition did the user read up to
        /// 2) How far into said partition did they read up to
        /// And since the client consumes queries strictly in a left to right order we can partition the documents:
        /// 1) Documents left of the continuation token have been drained
        /// 2) Documents to the right of the continuation token still need to be served.
        /// This is useful since we can have a single continuation token for all partitions.
        /// </summary>
        protected override string ContinuationToken
        {
            get
            {
                IEnumerable<ItemProducer> activeItemProducers = this.GetActiveItemProducers();
                string continuationToken;
                if (activeItemProducers.Any())
                {
                    IEnumerable<CompositeContinuationToken> compositeContinuationTokens = activeItemProducers.Select((documentProducer) => new CompositeContinuationToken
                    {
                        Token = documentProducer.CurrentContinuationToken,
                        Range = documentProducer.PartitionKeyRange.ToRange()
                    });
                    continuationToken = JsonConvert.SerializeObject(compositeContinuationTokens, DefaultJsonSerializationSettings.Value);
                }
                else
                {
                    continuationToken = null;
                }

                return continuationToken;
            }
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            IEnumerable<ItemProducer> activeItemProducers = this.GetActiveItemProducers();
            if (!activeItemProducers.Any())
            {
                return default;
            }

            List<CosmosElement> compositeContinuationTokens = new List<CosmosElement>();
            foreach (ItemProducer activeItemProducer in activeItemProducers)
            {
                CompositeContinuationToken compositeToken = new CompositeContinuationToken()
                {
                    Token = activeItemProducer.CurrentContinuationToken,
                    Range = new Documents.Routing.Range<string>(
                        min: activeItemProducer.PartitionKeyRange.MinInclusive,
                        max: activeItemProducer.PartitionKeyRange.MaxExclusive,
                        isMinInclusive: false,
                        isMaxInclusive: true)
                };

                CosmosElement compositeContinuationToken = CompositeContinuationToken.ToCosmosElement(compositeToken);
                compositeContinuationTokens.Add(compositeContinuationToken);
            }

            return CosmosArray.Create(compositeContinuationTokens);
        }

        /// <summary>
        /// Comparer used to determine if we should return the continuation token to the user
        /// </summary>
        /// <remarks>This basically just says that the two object are never equals, so that we don't return a continuation for a partition we have started draining.</remarks>
        private sealed class ParallelEqualityComparer : IEqualityComparer<CosmosElement>
        {
            /// <summary>
            /// Returns whether two parallel query items are equal.
            /// </summary>
            /// <param name="x">The first item.</param>
            /// <param name="y">The second item.</param>
            /// <returns>Whether two parallel query items are equal.</returns>
            public bool Equals(CosmosElement x, CosmosElement y)
            {
                return x == y;
            }

            /// <summary>
            /// Gets the hash code of an object.
            /// </summary>
            /// <param name="obj">The object to hash.</param>
            /// <returns>The hash code for the object.</returns>
            public int GetHashCode(CosmosElement obj)
            {
                return obj == null ? 0 : obj.GetHashCode();
            }
        }
    }
}
