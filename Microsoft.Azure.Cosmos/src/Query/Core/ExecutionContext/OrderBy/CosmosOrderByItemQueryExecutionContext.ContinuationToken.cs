namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;

    internal sealed partial class CosmosOrderByItemQueryExecutionContext
    {
        /// <summary>
        /// Gets the continuation token for an order by query.
        /// </summary>
        protected override string ContinuationToken
        {
            // In general the continuation token for order by queries contains the following information:
            // 1) What partition did we leave off on
            // 2) What value did we leave off 
            // Along with the constraints that we get from how we drain the documents:
            //      Let <x, y> mean that the last item we drained was item x from partition y.
            //      Then we know that for all partitions
            //          * < y that we have drained all items <= x
            //          * > y that we have drained all items < x
            //          * = y that we have drained all items <= x based on the backend continuation token for y
            // With this information we have captured the progress for all partitions in a single continuation token.
            get
            {
                IEnumerable<ItemProducer> activeItemProducers = this.GetActiveItemProducers();
                string continuationToken;
                if (activeItemProducers.Any())
                {
                    IEnumerable<CosmosElement> orderByContinuationTokens = activeItemProducers.Select((itemProducer) =>
                    {
                        OrderByQueryResult orderByQueryResult = new OrderByQueryResult(itemProducer.Current);
                        string filter = itemProducer.Filter;
                        OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                            new CompositeContinuationToken
                            {
                                Token = itemProducer.PreviousContinuationToken,
                                Range = itemProducer.PartitionKeyRange.ToRange(),
                            },
                            orderByQueryResult.OrderByItems,
                            orderByQueryResult.Rid,
                            this.ShouldIncrementSkipCount(itemProducer) ? this.skipCount + 1 : 0,
                            filter);

                        return OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
                    });

                    continuationToken = CosmosArray.Create(orderByContinuationTokens).ToString();
                }
                else
                {
                    continuationToken = null;
                }

                // Note we are no longer escaping non ascii continuation tokens.
                // It is the callers job to encode a continuation token before adding it to a header in their service.

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

            List<CosmosElement> orderByContinuationTokens = new List<CosmosElement>();
            foreach (ItemProducer activeItemProducer in activeItemProducers)
            {
                OrderByQueryResult orderByQueryResult = new OrderByQueryResult(activeItemProducer.Current);
                OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                    compositeContinuationToken: new CompositeContinuationToken()
                    {
                        Token = activeItemProducer.PreviousContinuationToken,
                        Range = new Documents.Routing.Range<string>(
                            min: activeItemProducer.PartitionKeyRange.MinInclusive,
                            max: activeItemProducer.PartitionKeyRange.MaxExclusive,
                            isMinInclusive: true,
                            isMaxInclusive: false)
                    },
                    orderByItems: orderByQueryResult.OrderByItems,
                    rid: orderByQueryResult.Rid,
                    skipCount: this.ShouldIncrementSkipCount(activeItemProducer) ? this.skipCount + 1 : 0,
                    filter: activeItemProducer.Filter);

                CosmosElement cosmosElementToken = OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
                orderByContinuationTokens.Add(cosmosElementToken);
            }

            return CosmosArray.Create(orderByContinuationTokens);
        }

        /// <summary>
        /// Equality comparer used to determine if a document producer needs it's continuation token returned.
        /// Basically just says that the continuation token can be flushed once you stop seeing duplicates.
        /// </summary>
        private sealed class OrderByEqualityComparer : IEqualityComparer<CosmosElement>
        {
            /// <summary>
            /// The order by comparer.
            /// </summary>
            private readonly OrderByItemProducerTreeComparer orderByConsumeComparer;

            /// <summary>
            /// Initializes a new instance of the OrderByEqualityComparer class.
            /// </summary>
            /// <param name="orderByConsumeComparer">The order by consume comparer.</param>
            public OrderByEqualityComparer(OrderByItemProducerTreeComparer orderByConsumeComparer)
            {
                this.orderByConsumeComparer = orderByConsumeComparer ?? throw new ArgumentNullException($"{nameof(orderByConsumeComparer)} can not be null.");
            }

            /// <summary>
            /// Gets whether two OrderByQueryResult instances are equal.
            /// </summary>
            /// <param name="x">The first.</param>
            /// <param name="y">The second.</param>
            /// <returns>Whether two OrderByQueryResult instances are equal.</returns>
            public bool Equals(CosmosElement x, CosmosElement y)
            {
                OrderByQueryResult orderByQueryResultX = new OrderByQueryResult(x);
                OrderByQueryResult orderByQueryResultY = new OrderByQueryResult(y);
                return this.orderByConsumeComparer.CompareOrderByItems(
                    orderByQueryResultX.OrderByItems,
                    orderByQueryResultY.OrderByItems) == 0;
            }

            /// <summary>
            /// Gets the hash code for object.
            /// </summary>
            /// <param name="obj">The object to hash.</param>
            /// <returns>The hash code for the OrderByQueryResult object.</returns>
            public int GetHashCode(CosmosElement obj)
            {
                return 0;
            }
        }
    }
}
