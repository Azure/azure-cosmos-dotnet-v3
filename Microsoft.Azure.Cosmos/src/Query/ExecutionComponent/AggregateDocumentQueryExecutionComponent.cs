//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.Aggregation;

    internal sealed class AggregateDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private readonly IAggregator[] aggregators;

        private AggregateDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, AggregateOperator[] aggregateOperators)
            : base(source)
        {
            this.aggregators = new IAggregator[aggregateOperators.Length];
            for (int i = 0; i < aggregateOperators.Length; ++i)
            {
                switch (aggregateOperators[i])
                {
                    case AggregateOperator.Average:
                        this.aggregators[i] = new AverageAggregator();
                        break;
                    case AggregateOperator.Count:
                        this.aggregators[i] = new CountAggregator();
                        break;
                    case AggregateOperator.Max:
                        this.aggregators[i] = new MinMaxAggregator(false);
                        break;
                    case AggregateOperator.Min:
                        this.aggregators[i] = new MinMaxAggregator(true);
                        break;
                    case AggregateOperator.Sum:
                        this.aggregators[i] = new SumAggregator();
                        break;
                    default:
                        string errorMessage = "Unexpected value: " + aggregateOperators[i].ToString();
                        Debug.Assert(false, errorMessage);
                        throw new InvalidProgramException(errorMessage);
                }
            }
        }

        public static async Task<AggregateDocumentQueryExecutionComponent> CreateAsync(
            AggregateOperator[] aggregateOperators,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback)
        {
            return new AggregateDocumentQueryExecutionComponent(await createSourceCallback(requestContinuation), aggregateOperators);
        }

        public override async Task<FeedResponse<object>> DrainAsync(int maxElements, CancellationToken token)
        {
            // Note-2016-10-25-felixfan: Given what we support now, we should expect to return only 1 document.
            double requestCharge = 0;
            long responseLengthBytes = 0;
            List<Uri> replicaUris = new List<Uri>();
            ClientSideRequestStatistics requestStatistics = new ClientSideRequestStatistics();

            while (!this.IsDone)
            {
                FeedResponse<object> result = await base.DrainAsync(int.MaxValue, token);
                requestCharge += result.RequestCharge;
                responseLengthBytes += result.ResponseLengthBytes;

                if (result.RequestStatistics != null)
                {
                    replicaUris.AddRange(result.RequestStatistics.ContactedReplicas);
                }

                foreach (dynamic item in result)
                {
                    AggregateItem[] values = (AggregateItem[])item;
                    Debug.Assert(values.Length == this.aggregators.Length, string.Format(
                        CultureInfo.InvariantCulture,
                        "Expect {0} values, but received {1}.",
                        this.aggregators.Length,
                        values.Length));

                    for (int i = 0; i < this.aggregators.Length; ++i)
                    {
                        this.aggregators[i].Aggregate(values[i].GetItem());
                    }
                }
            }

            List<object> finalResult = this.BindAggregateResults(
                this.aggregators.Select(aggregator => aggregator.GetResult()).ToArray());

            // The replicaUris may have duplicates.
            requestStatistics.ContactedReplicas.AddRange(replicaUris);

            return new FeedResponse<object>(
                finalResult,
                finalResult.Count,
                new StringKeyValueCollection() { { HttpConstants.HttpHeaders.RequestCharge, requestCharge.ToString(CultureInfo.InvariantCulture) } },
                requestStatistics,
                responseLengthBytes);
        }

        private List<object> BindAggregateResults(object[] aggregateResults)
        {
            // Note-2016-11-08-felixfan: Given what we support now, we should expect aggregateResults.Length == 1.
            string assertMessage = "Only support binding 1 aggregate function to projection.";
            Debug.Assert(this.aggregators.Length == 1, assertMessage);
            if (this.aggregators.Length != 1)
            {
                throw new NotSupportedException(assertMessage);
            }

            List<object> result = new List<object>();

            if (!Undefined.Value.Equals(aggregateResults[0]))
            {
                result.Add(aggregateResults[0]);
            }

            return result;
        }
    }
}
