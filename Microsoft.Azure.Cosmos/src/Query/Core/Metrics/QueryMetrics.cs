//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Query metrics in the Azure Cosmos database service.
    /// This metric represents a moving average for a set of queries whose metrics have been aggregated together.
    /// </summary>
    internal sealed class QueryMetrics
    {
        /// <summary>
        /// QueryMetrics that with all members having default (but not null) members.
        /// </summary>
        internal static readonly QueryMetrics Zero = new QueryMetrics(
            retrievedDocumentCount: 0,
            retrievedDocumentSize: 0,
            outputDocumentCount: 0,
            outputDocumentSize: 0,
            indexHitDocumentCount: 0,
            indexUtilizationInfo: IndexUtilizationInfo.Empty,
            totalQueryExecutionTime: default,
            queryPreparationTimes: QueryPreparationTimes.Zero,
            indexLookupTime: default,
            documentLoadTime: default,
            vmExecutionTime: default,
            runtimeExecutionTimes: RuntimeExecutionTimes.Zero,
            documentWriteTime: default,
            clientSideMetrics: ClientSideMetrics.Zero);

        /// <summary>
        /// Initializes a new instance of the QueryMetrics class.
        /// </summary>
        /// <param name="retrievedDocumentCount">Retrieved Document Count</param>
        /// <param name="retrievedDocumentSize">Retrieved Document Size</param>
        /// <param name="outputDocumentCount">Output Document Count</param>
        /// <param name="outputDocumentSize">Output Document Size</param>
        /// <param name="indexHitDocumentCount">Index Hit DocumentCount</param>
        /// <param name="indexUtilizationInfo">Index Utilization count</param>
        /// <param name="totalQueryExecutionTime">Total Query Execution Time</param>
        /// <param name="queryPreparationTimes">Query Preparation Times</param>
        /// <param name="indexLookupTime">Time spent in physical index layer.</param>
        /// <param name="documentLoadTime">Time spent in loading documents.</param>
        /// <param name="vmExecutionTime">Time spent in VM execution.</param>
        /// <param name="runtimeExecutionTimes">Runtime Execution Times</param>
        /// <param name="documentWriteTime">Time spent writing output document</param>
        /// <param name="clientSideMetrics">Client Side Metrics</param>
        [JsonConstructor]
        internal QueryMetrics(
            long retrievedDocumentCount,
            long retrievedDocumentSize,
            long outputDocumentCount,
            long outputDocumentSize,
            long indexHitDocumentCount,
            IndexUtilizationInfo indexUtilizationInfo,
            TimeSpan totalQueryExecutionTime,
            QueryPreparationTimes queryPreparationTimes,
            TimeSpan indexLookupTime,
            TimeSpan documentLoadTime,
            TimeSpan vmExecutionTime,
            RuntimeExecutionTimes runtimeExecutionTimes,
            TimeSpan documentWriteTime,
            ClientSideMetrics clientSideMetrics)
        {
            if (queryPreparationTimes == null)
            {
                throw new ArgumentNullException($"{nameof(queryPreparationTimes)} can not be null.");
            }

            if (runtimeExecutionTimes == null)
            {
                throw new ArgumentNullException($"{nameof(runtimeExecutionTimes)} can not be null.");
            }

            if (clientSideMetrics == null)
            {
                throw new ArgumentNullException($"{nameof(clientSideMetrics)} can not be null.");
            }

            this.RetrievedDocumentCount = retrievedDocumentCount;
            this.RetrievedDocumentSize = retrievedDocumentSize;
            this.OutputDocumentCount = outputDocumentCount;
            this.OutputDocumentSize = outputDocumentSize;
            this.IndexHitDocumentCount = indexHitDocumentCount;
            this.IndexUtilizationInfo = indexUtilizationInfo;
            this.TotalTime = totalQueryExecutionTime;
            this.QueryPreparationTimes = queryPreparationTimes;
            this.IndexLookupTime = indexLookupTime;
            this.DocumentLoadTime = documentLoadTime;
            this.VMExecutionTime = vmExecutionTime;
            this.RuntimeExecutionTimes = runtimeExecutionTimes;
            this.DocumentWriteTime = documentWriteTime;
            this.ClientSideMetrics = clientSideMetrics;
            this.QueryEngineTimes = new QueryEngineTimes(indexLookupTime, documentLoadTime, vmExecutionTime, documentWriteTime, runtimeExecutionTimes);
        }

        /// <summary>
        /// Gets the total query time in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan TotalTime { get; }

        /// <summary>
        /// Gets the number of documents retrieved during query in the Azure Cosmos database service.
        /// </summary>
        public long RetrievedDocumentCount { get; }

        /// <summary>
        /// Gets the size of documents retrieved in bytes during query in the Azure Cosmos DB service.
        /// </summary>
        public long RetrievedDocumentSize { get; }

        /// <summary>
        /// Gets the number of documents returned by query in the Azure Cosmos DB service.
        /// </summary>
        public long OutputDocumentCount { get; }

        /// <summary>
        /// Gets the size of documents outputted in bytes during query in the Azure Cosmos database service.
        /// </summary>
        internal long OutputDocumentSize { get; }

        /// <summary>
        /// Gets the total query time in the Azure Cosmos database service.
        /// </summary>
        internal TimeSpan TotalQueryExecutionTime => this.TotalTime;

        /// <summary>
        /// Gets the query QueryPreparationTimes in the Azure Cosmos database service.
        /// </summary>
        public QueryPreparationTimes QueryPreparationTimes { get; }

        /// <summary>
        /// Gets the <see cref="QueryEngineTimes"/> instance in the Azure Cosmos database service.
        /// </summary>
        public QueryEngineTimes QueryEngineTimes { get; }

        /// <summary>
        /// Gets number of reties in the Azure Cosmos database service.
        /// </summary>
        public long Retries => this.ClientSideMetrics.Retries;

        /// <summary>
        /// Gets the query index lookup time in the Azure Cosmos database service.
        /// </summary>
        internal TimeSpan IndexLookupTime { get; }

        /// <summary>
        /// Gets the document loading time during query in the Azure Cosmos database service.
        /// </summary>
        internal TimeSpan DocumentLoadTime { get; }

        /// <summary>
        /// Gets the query runtime execution times during query in the Azure Cosmos database service.
        /// </summary>
        internal RuntimeExecutionTimes RuntimeExecutionTimes { get; }

        /// <summary>
        /// Gets the output writing/serializing time during query in the Azure Cosmos database service.
        /// </summary>
        internal TimeSpan DocumentWriteTime { get; }

        /// <summary>
        /// Gets the <see cref="ClientSideMetrics"/> instance in the Azure Cosmos database service.
        /// </summary>
        [JsonProperty(PropertyName = "ClientSideMetrics")]
        internal ClientSideMetrics ClientSideMetrics { get; }

        /// <summary>
        /// Gets the index hit ratio by query in the Azure Cosmos database service.
        /// </summary>
        public double IndexHitRatio => this.RetrievedDocumentCount == 0
                    ? 1
                    : (double)this.IndexHitDocumentCount / this.RetrievedDocumentCount;

        /// <summary>
        /// Gets the Index Hit Document Count.
        /// </summary>
        internal long IndexHitDocumentCount { get; }

        /// <summary>
        /// Gets the Index Utilization information.
        /// </summary>
        internal IndexUtilizationInfo IndexUtilizationInfo { get; }

        /// <summary>
        /// Gets the VMExecution Time.
        /// </summary>
        internal TimeSpan VMExecutionTime { get; }

        /// <summary>
        /// Gets the Index Utilization.
        /// </summary>
        private double IndexUtilization => this.IndexHitRatio * 100;

        /// <summary>
        /// Add two specified <see cref="QueryMetrics"/> instances
        /// </summary>
        /// <param name="queryMetrics1">The first <see cref="QueryMetrics"/> instance</param>
        /// <param name="queryMetrics2">The second <see cref="QueryMetrics"/> instance</param>
        /// <returns>A new <see cref="QueryMetrics"/> instance that is the sum of two <see cref="QueryMetrics"/> instances</returns>
        public static QueryMetrics operator +(QueryMetrics queryMetrics1, QueryMetrics queryMetrics2)
        {
            return queryMetrics1.Add(queryMetrics2);
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
        internal string ToDelimitedString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            QueryMetricsDelimitedStringWriter queryMetricsDelimitedStringWriter = new QueryMetricsDelimitedStringWriter(stringBuilder);
            queryMetricsDelimitedStringWriter.WriteQueryMetrics(this);
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Creates a new QueryMetrics that is the sum of all elements in an IEnumerable.
        /// </summary>
        /// <param name="queryMetricsList">The IEnumerable to aggregate.</param>
        /// <returns>A new QueryMetrics that is the sum of all elements in an IEnumerable.</returns>
        internal static QueryMetrics CreateFromIEnumerable(IEnumerable<QueryMetrics> queryMetricsList)
        {
            if (queryMetricsList == null)
            {
                throw new ArgumentNullException("queryMetricsList");
            }

            long retrievedDocumentCount = 0;
            long retrievedDocumentSize = 0;
            long outputDocumentCount = 0;
            long outputDocumentSize = 0;
            long indexHitDocumentCount = 0;
            List<IndexUtilizationInfo> indexUtilizationInfoList = new List<IndexUtilizationInfo>();
            TimeSpan totalQueryExecutionTime = new TimeSpan();
            List<QueryPreparationTimes> queryPreparationTimesList = new List<QueryPreparationTimes>();
            TimeSpan indexLookupTime = new TimeSpan();
            TimeSpan documentLoadTime = new TimeSpan();
            TimeSpan vmExecutionTime = new TimeSpan();
            List<RuntimeExecutionTimes> runtimeExecutionTimesList = new List<RuntimeExecutionTimes>();
            TimeSpan documentWriteTime = new TimeSpan();
            List<ClientSideMetrics> clientSideMetricsList = new List<ClientSideMetrics>();

            foreach (QueryMetrics queryMetrics in queryMetricsList)
            {
                if (queryMetrics == null)
                {
                    throw new ArgumentNullException("queryMetricsList can not have null elements");
                }

                retrievedDocumentCount += queryMetrics.RetrievedDocumentCount;
                retrievedDocumentSize += queryMetrics.RetrievedDocumentSize;
                outputDocumentCount += queryMetrics.OutputDocumentCount;
                outputDocumentSize += queryMetrics.OutputDocumentSize;
                indexHitDocumentCount += queryMetrics.IndexHitDocumentCount;
                indexUtilizationInfoList.Add(queryMetrics.IndexUtilizationInfo);
                totalQueryExecutionTime += queryMetrics.TotalTime;
                queryPreparationTimesList.Add(queryMetrics.QueryPreparationTimes);
                indexLookupTime += queryMetrics.IndexLookupTime;
                documentLoadTime += queryMetrics.DocumentLoadTime;
                vmExecutionTime += queryMetrics.VMExecutionTime;
                runtimeExecutionTimesList.Add(queryMetrics.RuntimeExecutionTimes);
                documentWriteTime += queryMetrics.DocumentWriteTime;
                clientSideMetricsList.Add(queryMetrics.ClientSideMetrics);
            }

            return new QueryMetrics(
                retrievedDocumentCount,
                retrievedDocumentSize,
                outputDocumentCount,
                outputDocumentSize,
                indexHitDocumentCount,
                IndexUtilizationInfo.CreateFromIEnumerable(indexUtilizationInfoList),
                totalQueryExecutionTime,
                QueryPreparationTimes.CreateFromIEnumerable(queryPreparationTimesList),
                indexLookupTime,
                documentLoadTime,
                vmExecutionTime,
                RuntimeExecutionTimes.CreateFromIEnumerable(runtimeExecutionTimesList),
                documentWriteTime,
                ClientSideMetrics.CreateFromIEnumerable(clientSideMetricsList));
        }

        /// <summary>
        /// Creates a new QueryMetrics from the backend delimited string.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <param name="indexUtilization">The index utilization from the backend</param>
        /// <returns>A new QueryMetrics from the backend delimited string.</returns>
        internal static QueryMetrics CreateFromDelimitedString(string delimitedString, string indexUtilization)
        {
            return QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics(delimitedString, indexUtilization, new ClientSideMetrics(0, 0, new List<FetchExecutionRange>()));
        }

        /// <summary>
        /// Creates a new QueryMetrics from the backend delimited string and ClientSideMetrics.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <param name="indexUtilization">The index utilization from the backend</param>
        /// <param name="clientSideMetrics">The additional client side metrics.</param>
        /// <returns>A new QueryMetrics.</returns>
        internal static QueryMetrics CreateFromDelimitedStringAndClientSideMetrics(string delimitedString, string indexUtilization, ClientSideMetrics clientSideMetrics)
        {
            Dictionary<string, double> metrics = QueryMetricsUtils.ParseDelimitedString(delimitedString);
            metrics.TryGetValue(QueryMetricsConstants.IndexHitRatio, out double indexHitRatio);
            metrics.TryGetValue(QueryMetricsConstants.RetrievedDocumentCount, out double retrievedDocumentCount);
            long indexHitCount = (long)(indexHitRatio * retrievedDocumentCount);
            metrics.TryGetValue(QueryMetricsConstants.OutputDocumentCount, out double outputDocumentCount);
            metrics.TryGetValue(QueryMetricsConstants.OutputDocumentSize, out double outputDocumentSize);
            metrics.TryGetValue(QueryMetricsConstants.RetrievedDocumentSize, out double retrievedDocumentSize);
            TimeSpan totalQueryExecutionTime = QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.TotalQueryExecutionTimeInMs);

            IndexUtilizationInfo.TryCreateFromDelimitedString(indexUtilization, out IndexUtilizationInfo indexUtilizationInfo);
            return new QueryMetrics(
                (long)retrievedDocumentCount,
                (long)retrievedDocumentSize,
                (long)outputDocumentCount,
                (long)outputDocumentSize,
                indexHitCount,
                indexUtilizationInfo,
                totalQueryExecutionTime,
                QueryPreparationTimes.CreateFromDelimitedString(delimitedString),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.IndexLookupTimeInMs),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.DocumentLoadTimeInMs),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.VMExecutionTimeInMs),
                RuntimeExecutionTimes.CreateFromDelimitedString(delimitedString),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.DocumentWriteTimeInMs),
                clientSideMetrics);
        }

        internal static QueryMetrics CreateWithSchedulingMetrics(
            QueryMetrics queryMetrics)
        {
            return new QueryMetrics(
                queryMetrics.RetrievedDocumentCount,
                queryMetrics.RetrievedDocumentSize,
                queryMetrics.OutputDocumentCount,
                queryMetrics.OutputDocumentSize,
                queryMetrics.IndexHitDocumentCount,
                queryMetrics.IndexUtilizationInfo,
                queryMetrics.TotalQueryExecutionTime,
                queryMetrics.QueryPreparationTimes,
                queryMetrics.IndexLookupTime,
                queryMetrics.DocumentLoadTime,
                queryMetrics.VMExecutionTime,
                queryMetrics.RuntimeExecutionTimes,
                queryMetrics.DocumentWriteTime,
                new ClientSideMetrics(
                    queryMetrics.ClientSideMetrics.Retries,
                    queryMetrics.ClientSideMetrics.RequestCharge,
                    queryMetrics.ClientSideMetrics.FetchExecutionRanges));
        }

        /// <summary>
        /// Adds all QueryMetrics in a list along with the current instance.
        /// </summary>
        /// <param name="queryMetricsList">The list to sum up.</param>
        /// <returns>A new QueryMetrics instance that is the sum of the current instance and the list.</returns>
        internal QueryMetrics Add(params QueryMetrics[] queryMetricsList)
        {
            List<QueryMetrics> combinedQueryMetricsList = new List<QueryMetrics>(queryMetricsList.Length + 1)
            {
                this
            };
            combinedQueryMetricsList.AddRange(queryMetricsList);
            return QueryMetrics.CreateFromIEnumerable(combinedQueryMetricsList);
        }
    }

    #region QueryEngineTimes

    /// <summary>
    /// Query engine time in the Azure Cosmos database service.
    /// (dummy class that will be deprecated).
    /// </summary>
    internal sealed class QueryEngineTimes
    {
        internal QueryEngineTimes(TimeSpan indexLookupTime, TimeSpan documentLoadTime, TimeSpan vmExecutionTime, TimeSpan writeOutputTime, RuntimeExecutionTimes runtimeExecutionTimes)
        {
            this.IndexLookupTime = indexLookupTime;
            this.DocumentLoadTime = documentLoadTime;
            this.VMExecutionTime = vmExecutionTime;
            this.WriteOutputTime = writeOutputTime;
            this.RuntimeExecutionTimes = runtimeExecutionTimes;
        }

        /// <summary>
        /// Gets the query index lookup time in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan IndexLookupTime { get; }

        /// <summary>
        /// Gets the document loading time during query in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan DocumentLoadTime { get; }

        /// <summary>
        /// Gets the output writing/serializing time during query in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan WriteOutputTime { get; }

        /// <summary>
        /// Gets the query runtime execution times during query in the Azure Cosmos database service.
        /// </summary>
        public RuntimeExecutionTimes RuntimeExecutionTimes { get; }

        internal TimeSpan VMExecutionTime { get; }
    }
    #endregion
}