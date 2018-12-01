//-----------------------------------------------------------------------
// <copyright file="QueryMetrics.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Query metrics in the Azure DocumentDB database service.
    /// This metric represents a moving average for a set of queries whose metrics have been aggregated together.
    /// </summary>
    internal sealed class QueryMetrics
    {
        /// <summary>
        /// QueryMetrics that with all members having default (but not null) members.
        /// </summary>
        internal static readonly QueryMetrics Zero = new QueryMetrics(new List<Guid>(), 0, 0, 0, 0, 0, default(TimeSpan), QueryPreparationTimes.Zero, default(TimeSpan), default(TimeSpan), default(TimeSpan), RuntimeExecutionTimes.Zero, default(TimeSpan), ClientSideMetrics.Zero);

        /// <summary>
        /// Initializes a new instance of the QueryMetrics class.
        /// </summary>
        /// <param name="activityIds">Activity Ids.</param>
        /// <param name="retrievedDocumentCount">Retrieved Document Count</param>
        /// <param name="retrievedDocumentSize">Retrieved Document Size</param>
        /// <param name="outputDocumentCount">Output Document Count</param>
        /// <param name="outputDocumentSize">Output Document Size</param>
        /// <param name="indexHitDocumentCount">Index Hit DocumentCount</param>
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
            List<Guid> activityIds,
            long retrievedDocumentCount,
            long retrievedDocumentSize,
            long outputDocumentCount,
            long outputDocumentSize,
            long indexHitDocumentCount,
            TimeSpan totalQueryExecutionTime,
            QueryPreparationTimes queryPreparationTimes,
            TimeSpan indexLookupTime,
            TimeSpan documentLoadTime,
            TimeSpan vmExecutionTime,
            RuntimeExecutionTimes runtimeExecutionTimes,
            TimeSpan documentWriteTime, 
            ClientSideMetrics clientSideMetrics)
        {
            if (activityIds == null)
            {
                throw new ArgumentNullException($"{nameof(activityIds)} can not be null.");
            }

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

            this.ActivityIds = activityIds;
            this.RetrievedDocumentCount = retrievedDocumentCount;
            this.RetrievedDocumentSize = retrievedDocumentSize;
            this.OutputDocumentCount = outputDocumentCount;
            this.OutputDocumentSize = outputDocumentSize;
            this.IndexHitDocumentCount = indexHitDocumentCount;
            this.TotalQueryExecutionTime = totalQueryExecutionTime;
            this.QueryPreparationTimes = queryPreparationTimes;
            this.IndexLookupTime = indexLookupTime;
            this.DocumentLoadTime = documentLoadTime;
            this.VMExecutionTime = vmExecutionTime;
            this.RuntimeExecutionTimes = runtimeExecutionTimes;
            this.DocumentWriteTime = documentWriteTime;
            this.ClientSideMetrics = clientSideMetrics;
        }

        /// <summary>
        /// Gets the number of documents retrieved during query in the Azure DocumentDB database service.
        /// </summary>
        internal long RetrievedDocumentCount
        {
            get;
        }

        /// <summary>
        /// Gets the size of documents retrieved in bytes during query in the Azure Cosmos DB service.
        /// </summary>
        internal long RetrievedDocumentSize
        {
            get;
        }

        /// <summary>
        /// Gets the number of documents returned by query in the Azure Cosmos DB service.
        /// </summary>
        internal long OutputDocumentCount
        {
            get;
        }

        /// <summary>
        /// Gets the size of documents outputted in bytes during query in the Azure DocumentDB database service.
        /// </summary>
        internal long OutputDocumentSize
        {
            get;
        }

        /// <summary>
        /// Gets the total query time in the Azure DocumentDB database service.
        /// </summary>
        internal TimeSpan TotalQueryExecutionTime
        {
            get;
        }

        /// <summary>
        /// Gets the query QueryPreparationTimes in the Azure DocumentDB database service.
        /// </summary>
        internal QueryPreparationTimes QueryPreparationTimes
        {
            get;
        }

        /// <summary>
        /// Gets the query index lookup time in the Azure DocumentDB database service.
        /// </summary>
        internal TimeSpan IndexLookupTime
        {
            get;
        }

        /// <summary>
        /// Gets the document loading time during query in the Azure DocumentDB database service.
        /// </summary>
        internal TimeSpan DocumentLoadTime
        {
            get;
        }

        /// <summary>
        /// Gets the query runtime execution times during query in the Azure DocumentDB database service.
        /// </summary>
        internal RuntimeExecutionTimes RuntimeExecutionTimes
        {
            get;
        }

        /// <summary>
        /// Gets the output writing/serializing time during query in the Azure DocumentDB database service.
        /// </summary>
        internal TimeSpan DocumentWriteTime
        {
            get;
        }

        /// <summary>
        /// Gets the <see cref="ClientSideMetrics"/> instance in the Azure DocumentDB database service.
        /// </summary>
        [JsonProperty(PropertyName = "ClientSideMetrics")]
        internal ClientSideMetrics ClientSideMetrics
        {
            get;
        }

        /// <summary>
        /// Gets the index hit ratio by query in the Azure DocumentDB database service.
        /// </summary>
        internal double IndexHitRatio
        {
            get
            {
                return this.RetrievedDocumentCount == 0
                    ? 1
                    : (double)this.IndexHitDocumentCount / this.RetrievedDocumentCount;
            }
        }

        /// <summary>
        /// Gets the Index Hit Document Count.
        /// </summary>
        internal long IndexHitDocumentCount
        {
            get;
        }

        /// <summary>
        /// Gets the VMExecution Time.
        /// </summary>
        internal TimeSpan VMExecutionTime
        {
            get;
        }

        /// <summary>
        /// Gets the Activity IDs for this QueryMetrics
        /// </summary>
        [JsonProperty(PropertyName = "ActivityIds")]
        internal IReadOnlyList<Guid> ActivityIds
        {
            get;
        }

        /// <summary>
        /// Gets the Index Utilization.
        /// </summary>
        internal double IndexUtilization
        {
            get
            {
                return this.IndexHitRatio * 100;
            }
        }

        /// <summary>
        /// Add two specified <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instances
        /// </summary>
        /// <param name="queryMetrics1">The first <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instance</param>
        /// <param name="queryMetrics2">The second <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instance</param>
        /// <returns>A new <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instance that is the sum of two <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instances</returns>
        public static QueryMetrics operator +(QueryMetrics queryMetrics1, QueryMetrics queryMetrics2)
        {
            return queryMetrics1.Add(queryMetrics2);
        }

        /// <summary>
        /// Gets the stringified <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instance in the Azure DocumentDB database service.
        /// </summary>
        /// <returns>The stringified <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instance in the Azure DocumentDB database service.</returns>
        public override string ToString()
        {
            return this.ToTextString();
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

            List<Guid> activityIds = new List<Guid>();
            long retrievedDocumentCount = 0;
            long retrievedDocumentSize = 0;
            long outputDocumentCount = 0;
            long outputDocumentSize = 0;
            long indexHitDocumentCount = 0;
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

                activityIds = activityIds.Concat(queryMetrics.ActivityIds.Where((activityId) => activityId != Guid.Empty)).ToList();
                retrievedDocumentCount += queryMetrics.RetrievedDocumentCount;
                retrievedDocumentSize += queryMetrics.RetrievedDocumentSize;
                outputDocumentCount += queryMetrics.OutputDocumentCount;
                outputDocumentSize += queryMetrics.OutputDocumentSize;
                indexHitDocumentCount += queryMetrics.IndexHitDocumentCount;
                totalQueryExecutionTime += queryMetrics.TotalQueryExecutionTime;
                queryPreparationTimesList.Add(queryMetrics.QueryPreparationTimes);
                indexLookupTime += queryMetrics.IndexLookupTime;
                documentLoadTime += queryMetrics.DocumentLoadTime;
                vmExecutionTime += queryMetrics.VMExecutionTime;
                runtimeExecutionTimesList.Add(queryMetrics.RuntimeExecutionTimes);
                documentWriteTime += queryMetrics.DocumentWriteTime;
                clientSideMetricsList.Add(queryMetrics.ClientSideMetrics);
            }

            return new QueryMetrics(
                activityIds,
                retrievedDocumentCount,
                retrievedDocumentSize,
                outputDocumentCount,
                outputDocumentSize,
                indexHitDocumentCount,
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
        /// <returns>A new QueryMetrics from the backend delimited string.</returns>
        internal static QueryMetrics CreateFromDelimitedString(string delimitedString)
        {
            return QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics(delimitedString, new ClientSideMetrics(0, 0, new List<FetchExecutionRange>(), new List<Tuple<string, SchedulingTimeSpan>>()), Guid.Empty);
        }

        /// <summary>
        /// Creates a new QueryMetrics from the backend delimited string and ClientSideMetrics.
        /// </summary>
        /// <param name="delimitedString">The backend delimited string to deserialize from.</param>
        /// <param name="clientSideMetrics">The additional client side metrics.</param>
        /// <param name="activityId">The ActivityId.</param>
        /// <returns>A new QueryMetrics.</returns>
        internal static QueryMetrics CreateFromDelimitedStringAndClientSideMetrics(string delimitedString, ClientSideMetrics clientSideMetrics, Guid activityId)
        {
            Dictionary<string, double> metrics = QueryMetricsUtils.ParseDelimitedString(delimitedString);
            double indexHitRatio;
            double retrievedDocumentCount;
            metrics.TryGetValue(QueryMetricsConstants.IndexHitRatio, out indexHitRatio);
            metrics.TryGetValue(QueryMetricsConstants.RetrievedDocumentCount, out retrievedDocumentCount);
            long indexHitCount = (long)(indexHitRatio * retrievedDocumentCount);
            double outputDocumentCount;
            metrics.TryGetValue(QueryMetricsConstants.OutputDocumentCount, out outputDocumentCount);
            double outputDocumentSize;
            metrics.TryGetValue(QueryMetricsConstants.OutputDocumentSize, out outputDocumentSize);
            double retrievedDocumentSize;
            metrics.TryGetValue(QueryMetricsConstants.RetrievedDocumentSize, out retrievedDocumentSize);
            TimeSpan totalQueryExecutionTime = QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.TotalQueryExecutionTimeInMs);

            return new QueryMetrics(
                new List<Guid>() { activityId },
                (long)retrievedDocumentCount,
                (long)retrievedDocumentSize,
                (long)outputDocumentCount,
                (long)outputDocumentSize,
                indexHitCount,
                totalQueryExecutionTime,
                QueryPreparationTimes.CreateFromDelimitedString(delimitedString),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.IndexLookupTimeInMs),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.DocumentLoadTimeInMs),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.VMExecutionTimeInMs),
                RuntimeExecutionTimes.CreateFromDelimitedString(delimitedString),
                QueryMetricsUtils.TimeSpanFromMetrics(metrics, QueryMetricsConstants.DocumentWriteTimeInMs),
                clientSideMetrics);
        }

        /// <summary>
        /// Gets a human readable plain text of the QueryMetrics (Please use monospace font).
        /// </summary>
        /// <param name="indentLevel">The level of nesting / indenting of this object.</param>
        /// <returns>A human readable plain text of the QueryMetrics.</returns>
        internal string ToTextString(int indentLevel = 0)
        {
            if (indentLevel == int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("indentLevel", "input must be less than Int32.MaxValue");
            }

            StringBuilder stringBuilder = new StringBuilder();
            checked
            {
                // Top level properties
                QueryMetricsUtils.AppendCountToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.RetrievedDocumentCountText,
                    this.RetrievedDocumentCount,
                    indentLevel);

                QueryMetricsUtils.AppendBytesToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.RetrievedDocumentSizeText,
                    this.RetrievedDocumentSize,
                    indentLevel);

                QueryMetricsUtils.AppendCountToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.OutputDocumentCountText,
                    this.OutputDocumentCount,
                    indentLevel);

                QueryMetricsUtils.AppendBytesToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.OutputDocumentSizeText,
                    this.OutputDocumentSize,
                    indentLevel);

                QueryMetricsUtils.AppendPercentageToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.IndexUtilizationText,
                    this.IndexHitRatio,
                    indentLevel);

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.TotalQueryExecutionTimeText,
                    this.TotalQueryExecutionTime.TotalMilliseconds,
                    indentLevel);

                // QueryPreparationTimes
                stringBuilder.Append(this.QueryPreparationTimes.ToTextString(indentLevel + 1));

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.IndexLookupTimeText,
                    this.IndexLookupTime.TotalMilliseconds,
                    indentLevel + 1);

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.DocumentLoadTimeText,
                    this.DocumentLoadTime.TotalMilliseconds,
                    indentLevel + 1);

                // VM Execution Time is not emitted since the user does not have any context

                // RuntimesExecutionTimes
                stringBuilder.Append(this.RuntimeExecutionTimes.ToTextString(indentLevel + 1));

                QueryMetricsUtils.AppendMillisecondsToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.WriteOutputTimeText,
                    this.DocumentWriteTime.TotalMilliseconds,
                    indentLevel + 1);

                // Client Side Metrics
                stringBuilder.Append(this.ClientSideMetrics.ToTextString(indentLevel + 1));

                // Activity Ids
                QueryMetricsUtils.AppendActivityIdsToStringBuilder(stringBuilder, QueryMetricsConstants.ActivityIds, this.ActivityIds, indentLevel);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Gets the json stringified <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instance in the Azure DocumentDB database service.
        /// </summary>
        /// <returns>The json stringified <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instance in the Azure DocumentDB database service.</returns>
        internal string ToJsonString()
        {
            string queryMetricString = JsonConvert.SerializeObject(this, Formatting.Indented);
            return queryMetricString;
        }

        /// <summary>
        /// Gets the delimited stringified <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instance in the Azure DocumentDB database service as if from a backend response.
        /// </summary>
        /// <returns>The delimited stringified <see cref="Microsoft.Azure.Cosmos.QueryMetrics"/> instance in the Azure DocumentDB database service as if from a backend response.</returns>
        internal string ToDelimitedString()
        {
            // TODO (brchon): use string builder.
            const string FormatString = "{0}={1};{2}={3};{4}={5};{6}={7};{8}={9};{10}={11:0.00};{12};{13}={14:0.00};{15}={16:0.00};{17}={18:0.00};{19};{20}={21:0.00}";
            return string.Format(
                CultureInfo.InvariantCulture,
                FormatString,
                QueryMetricsConstants.RetrievedDocumentCount,
                this.RetrievedDocumentCount,
                QueryMetricsConstants.RetrievedDocumentSize,
                this.RetrievedDocumentSize,
                QueryMetricsConstants.OutputDocumentCount,
                this.OutputDocumentCount,
                QueryMetricsConstants.OutputDocumentSize,
                this.OutputDocumentSize,
                QueryMetricsConstants.IndexHitRatio,
                this.IndexHitRatio,
                QueryMetricsConstants.TotalQueryExecutionTimeInMs,
                this.TotalQueryExecutionTime.TotalMilliseconds,
                this.QueryPreparationTimes.ToDelimitedString(),
                QueryMetricsConstants.IndexLookupTimeInMs,
                this.IndexLookupTime.TotalMilliseconds,
                QueryMetricsConstants.DocumentLoadTimeInMs,
                this.DocumentLoadTime.TotalMilliseconds,
                QueryMetricsConstants.VMExecutionTimeInMs,
                this.VMExecutionTime.TotalMilliseconds,
                this.RuntimeExecutionTimes.ToDelimitedString(),
                QueryMetricsConstants.DocumentWriteTimeInMs,
                this.DocumentWriteTime.TotalMilliseconds);
        }

        /// <summary>
        /// Adds all QueryMetrics in a list along with the current instance.
        /// </summary>
        /// <param name="queryMetricsList">The list to sum up.</param>
        /// <returns>A new QueryMetrics instance that is the sum of the current instance and the list.</returns>
        internal QueryMetrics Add(params QueryMetrics[] queryMetricsList)
        {
            List<QueryMetrics> combinedQueryMetricsList = new List<QueryMetrics>(queryMetricsList.Length + 1);
            combinedQueryMetricsList.Add(this);
            combinedQueryMetricsList.AddRange(queryMetricsList);
            return QueryMetrics.CreateFromIEnumerable(combinedQueryMetricsList);
        }
    }
}