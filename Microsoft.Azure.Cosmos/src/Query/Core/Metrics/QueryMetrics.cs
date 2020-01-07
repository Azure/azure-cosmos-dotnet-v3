//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Query metrics in the Azure Cosmos database service.
    /// This metric represents a moving average for a set of queries whose metrics have been aggregated together.
    /// </summary>
    internal sealed class QueryMetrics
    {
        /// <summary>
        /// QueryMetrics that with all members having default (but not null) members.
        /// </summary>
        internal static readonly QueryMetrics Empty = new QueryMetrics(
            backendMetrics: BackendMetrics.Empty,
            indexUtilizationInfo: IndexUtilizationInfo.Empty,
            clientSideMetrics: ClientSideMetrics.Empty);

        public QueryMetrics(
            BackendMetrics backendMetrics,
            IndexUtilizationInfo indexUtilizationInfo,
            ClientSideMetrics clientSideMetrics)
        {
            this.BackendMetrics = backendMetrics ?? throw new ArgumentNullException(nameof(backendMetrics));
            this.IndexUtilizationInfo = indexUtilizationInfo ?? throw new ArgumentNullException(nameof(indexUtilizationInfo));
            this.ClientSideMetrics = clientSideMetrics ?? throw new ArgumentNullException(nameof(clientSideMetrics));
        }

        public BackendMetrics BackendMetrics { get; }

        public IndexUtilizationInfo IndexUtilizationInfo { get; }

        public ClientSideMetrics ClientSideMetrics { get; }

        /// <summary>
        /// Add two specified <see cref="QueryMetrics"/> instances
        /// </summary>
        /// <param name="queryMetrics1">The first <see cref="QueryMetrics"/> instance</param>
        /// <param name="queryMetrics2">The second <see cref="QueryMetrics"/> instance</param>
        /// <returns>A new <see cref="QueryMetrics"/> instance that is the sum of two <see cref="QueryMetrics"/> instances</returns>
        public static QueryMetrics operator +(QueryMetrics queryMetrics1, QueryMetrics queryMetrics2)
        {
            QueryMetrics.Accumulator queryMetricsAccumulator = new QueryMetrics.Accumulator();
            queryMetricsAccumulator.Accumulate(queryMetrics1);
            queryMetricsAccumulator.Accumulate(queryMetrics2);

            return queryMetricsAccumulator.Finalize();
        }

        /// <summary>
        /// Gets the stringified <see cref="QueryMetrics"/> instance in the Azure Cosmos database service.
        /// </summary>
        /// <returns>The stringified <see cref="QueryMetrics"/> instance in the Azure Cosmos database service.</returns>
        public override string ToString()
        {
            return this.ToTextString();
        }

        private string ToTextString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            QueryMetricsTextWriter queryMetricsTextWriter = new QueryMetricsTextWriter(stringBuilder);
            queryMetricsTextWriter.WriteQueryMetrics(this);
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Gets the delimited stringified <see cref="QueryMetrics"/> instance in the Azure Cosmos database service as if from a backend response.
        /// </summary>
        /// <returns>The delimited stringified <see cref="QueryMetrics"/> instance in the Azure Cosmos database service as if from a backend response.</returns>
        private string ToDelimitedString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            QueryMetricsDelimitedStringWriter queryMetricsDelimitedStringWriter = new QueryMetricsDelimitedStringWriter(stringBuilder);
            queryMetricsDelimitedStringWriter.WriteQueryMetrics(this);
            return stringBuilder.ToString();
        }

        public ref struct Accumulator
        {
            public BackendMetrics.Accumulator BackendMetricsAccumulator { get; }

            public IndexUtilizationInfo IndexUtilizationInfo { get; }

            public ClientSideMetrics.Accumulator ClientSideMetricsAccumulator { get; }

            public void Accumulate(QueryMetrics queryMetrics)
            {
                if (queryMetrics == null)
                {
                    throw new ArgumentNullException(nameof(queryMetrics));
                }

                this.BackendMetricsAccumulator.Accumulate(queryMetrics.BackendMetrics);
                this.ClientSideMetricsAccumulator.Accumulate(queryMetrics.ClientSideMetrics);
            }

            public QueryMetrics Finalize()
            {
                return new QueryMetrics(
                    BackendMetrics.Accumulator.ToBackendMetrics(this.BackendMetricsAccumulator),
                    this.IndexUtilizationInfo,
                    this.ClientSideMetricsAccumulator.Finalize());
            }
        }

        /// <summary>
        /// Creates a new QueryMetrics that is the sum of all elements in an IEnumerable.
        /// </summary>
        /// <param name="queryMetricsList">The IEnumerable to aggregate.</param>
        /// <returns>A new QueryMetrics that is the sum of all elements in an IEnumerable.</returns>
        public static QueryMetrics CreateFromIEnumerable(IEnumerable<QueryMetrics> queryMetricsList)
        {
            if (queryMetricsList == null)
            {
                throw new ArgumentNullException(nameof(queryMetricsList));
            }

            QueryMetrics.Accumulator queryMetricsAccumulator = new QueryMetrics.Accumulator();

            foreach (QueryMetrics queryMetrics in queryMetricsList)
            {
                if (queryMetrics == null)
                {
                    throw new ArgumentNullException("queryMetricsList can not have null elements");
                }

                queryMetricsAccumulator.Accumulate(queryMetrics);
            }

            return queryMetricsAccumulator.Finalize();
        }
    }
}