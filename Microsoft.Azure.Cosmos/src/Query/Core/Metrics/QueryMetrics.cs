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
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class QueryMetrics
    {
        /// <summary>
        /// QueryMetrics that with all members having default (but not null) members.
        /// </summary>
        public static readonly QueryMetrics Empty = new QueryMetrics(
            deliminatedString: string.Empty,
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

        public QueryMetrics(
            string deliminatedString,
            IndexUtilizationInfo indexUtilizationInfo,
            ClientSideMetrics clientSideMetrics)
            : this(!String.IsNullOrWhiteSpace(deliminatedString) &&
                    BackendMetricsParser.TryParse(deliminatedString, out BackendMetrics backendMetrics)
                ? backendMetrics
                : BackendMetrics.Empty, indexUtilizationInfo, clientSideMetrics)
        {
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
            QueryMetricsAccumulator queryMetricsAccumulator = new QueryMetricsAccumulator();
            queryMetricsAccumulator.Accumulate(queryMetrics1);
            queryMetricsAccumulator.Accumulate(queryMetrics2);

            return queryMetricsAccumulator.GetQueryMetrics();
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

        public static QueryMetrics CreateFromIEnumerable(IEnumerable<QueryMetrics> queryMetricsList)
        {
            if (queryMetricsList == null)
            {
                throw new ArgumentNullException(nameof(queryMetricsList));
            }

            QueryMetricsAccumulator queryMetricsAccumulator = new QueryMetricsAccumulator();
            foreach (QueryMetrics queryMetrics in queryMetricsList)
            {
                queryMetricsAccumulator.Accumulate(queryMetrics);
            }

            return queryMetricsAccumulator.GetQueryMetrics();
        }
    }
}