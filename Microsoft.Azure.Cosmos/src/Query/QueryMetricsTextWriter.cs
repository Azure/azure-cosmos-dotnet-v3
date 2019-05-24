//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    internal sealed class QueryMetricsTextWriter : QueryMetricsWriter
    {
        #region Constants
        // QueryMetrics Text
        private const string ActivityIds = "Activity Ids";
        private const string RetrievedDocumentCount = "Retrieved Document Count";
        private const string RetrievedDocumentSize = "Retrieved Document Size";
        private const string OutputDocumentCount = "Output Document Count";
        private const string OutputDocumentSize = "Output Document Size";
        private const string IndexUtilization = "Index Utilization";
        private const string TotalQueryExecutionTime = "Total Query Execution Time";

        // QueryPreparationTimes 
        private const string QueryPreparationTimes = "Query Preparation Times";
        private const string QueryCompileTime = "Query Compilation Time";
        private const string LogicalPlanBuildTime = "Logical Plan Build Time";
        private const string PhysicalPlanBuildTime = "Physical Plan Build Time";
        private const string QueryOptimizationTime = "Query Optimization Time";

        // QueryTimes 
        private const string QueryEngineTimes = "Query Engine Times";
        private const string IndexLookupTime = "Index Lookup Time";
        private const string DocumentLoadTime = "Document Load Time";
        private const string DocumentWriteTime = "Document Write Time";

        // RuntimeExecutionTimes 
        private const string RuntimeExecutionTimes = "Runtime Execution Times";
        private const string TotalExecutionTime = "Query Engine Execution Time";
        private const string SystemFunctionExecuteTime = "System Function Execution Time";
        private const string UserDefinedFunctionExecutionTime = "User-defined Function Execution Time";

        // ClientSideQueryMetrics 
        private const string ClientSideQueryMetrics = "Client Side Metrics";
        private const string Retries = "Retry Count";
        private const string RequestCharge = "Request Charge";
        private const string FetchExecutionRanges = "Partition Execution Timeline";
        private const string SchedulingMetrics = "Scheduling Metrics";

        // Constants for Partition Execution Timeline Table
        private const string StartTimeHeader = "Start Time (UTC)";
        private const string EndTimeHeader = "End Time (UTC)";
        private const string DurationHeader = "Duration (ms)";
        private const string PartitionKeyRangeIdHeader = "Partition Id";
        private const string NumberOfDocumentsHeader = "Number of Documents";
        private const string RetryCountHeader = "Retry Count";
        private const string ActivityIdHeader = "Activity Id";

        // Constants for Scheduling Metrics Table
        private const string PartitionIdHeader = "Partition Id";
        private const string ResponseTimeHeader = "Response Time (ms)";
        private const string RunTimeHeader = "Run Time (ms)";
        private const string WaitTimeHeader = "Wait Time (ms)";
        private const string TurnaroundTimeHeader = "Turnaround Time (ms)";
        private const string NumberOfPreemptionHeader = "Number of Preemptions";

        private const string DateTimeFormat = "HH':'mm':'ss.ffff'Z'";

        // Static readonly for Partition Execution Timeline Table
        private static readonly int MaxDateTimeStringLength = DateTime.MaxValue.ToUniversalTime().ToString(DateTimeFormat).Length;
        private static readonly int StartTimeHeaderLength = Math.Max(MaxDateTimeStringLength, StartTimeHeader.Length);
        private static readonly int EndTimeHeaderLength = Math.Max(MaxDateTimeStringLength, EndTimeHeader.Length);
        private static readonly int DurationHeaderLength = Math.Max(DurationHeader.Length, TimeSpan.MaxValue.TotalMilliseconds.ToString("0.00").Length);
        private static readonly int PartitionKeyRangeIdHeaderLength = PartitionKeyRangeIdHeader.Length;
        private static readonly int NumberOfDocumentsHeaderLength = NumberOfDocumentsHeader.Length;
        private static readonly int RetryCountHeaderLength = RetryCountHeader.Length;
        private static readonly int ActivityIdHeaderLength = Guid.Empty.ToString().Length;

        private static readonly TextTable.Column[] PartitionExecutionTimelineColumns = new TextTable.Column[]
        {
            new TextTable.Column(PartitionKeyRangeIdHeader, PartitionKeyRangeIdHeaderLength),
            new TextTable.Column(ActivityIdHeader, ActivityIdHeaderLength),
            new TextTable.Column(StartTimeHeader, StartTimeHeaderLength),
            new TextTable.Column(EndTimeHeader, EndTimeHeaderLength),
            new TextTable.Column(DurationHeader, DurationHeaderLength),
            new TextTable.Column(NumberOfDocumentsHeader, NumberOfDocumentsHeaderLength),
            new TextTable.Column(RetryCountHeader, RetryCountHeaderLength),
        };

        private static readonly TextTable PartitionExecutionTimelineTable = new TextTable(PartitionExecutionTimelineColumns);

        // Static readonly for Scheduling Metrics Table
        private static readonly int MaxTimeSpanStringLength = Math.Max(TimeSpan.MaxValue.TotalMilliseconds.ToString("G17").Length, TurnaroundTimeHeader.Length);
        private static readonly int PartitionIdHeaderLength = PartitionIdHeader.Length;
        private static readonly int ResponseTimeHeaderLength = MaxTimeSpanStringLength;
        private static readonly int RunTimeHeaderLength = MaxTimeSpanStringLength;
        private static readonly int WaitTimeHeaderLength = MaxTimeSpanStringLength;
        private static readonly int TurnaroundTimeHeaderLength = MaxTimeSpanStringLength;
        private static readonly int NumberOfPreemptionHeaderLength = NumberOfPreemptionHeader.Length;

        private static readonly TextTable.Column[] SchedulingMetricsColumns = new TextTable.Column[]
        {
            new TextTable.Column(PartitionIdHeader, PartitionIdHeaderLength),
            new TextTable.Column(ResponseTimeHeader, ResponseTimeHeaderLength),
            new TextTable.Column(RunTimeHeader, RunTimeHeaderLength),
            new TextTable.Column(WaitTimeHeader, WaitTimeHeaderLength),
            new TextTable.Column(TurnaroundTimeHeader, TurnaroundTimeHeaderLength),
            new TextTable.Column(NumberOfPreemptionHeader, NumberOfPreemptionHeaderLength),
        };

        private static readonly TextTable SchedulingMetricsTable = new TextTable(SchedulingMetricsColumns);
        #endregion

        private readonly StringBuilder stringBuilder;

        // FetchExecutionRange state
        private string lastFetchPartitionId;
        private string lastActivityId;
        private DateTime lastStartTime;
        private DateTime lastEndTime;
        private long lastFetchDocumentCount;
        private long lastFetchRetryCount;

        // PartitionSchedulingTimeSpan state
        private string lastSchedulingPartitionId;
        private TimeSpan lastResponseTime;
        private TimeSpan lastRunTime;
        private TimeSpan lastWaitTime;
        private TimeSpan lastTurnaroundTime;
        private long lastNumberOfPreemptions;

        public QueryMetricsTextWriter(StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
            {
                throw new ArgumentNullException($"{nameof(stringBuilder)} must not be null.");
            }

            this.stringBuilder = stringBuilder;
        }

        protected override void WriteBeforeQueryMetrics()
        {
            // Do Nothing
        }

        protected override void WriteRetrievedDocumentCount(long retrievedDocumentCount)
        {
            QueryMetricsTextWriter.AppendCountToStringBuilder(stringBuilder, QueryMetricsTextWriter.RetrievedDocumentCount, retrievedDocumentCount, indentLevel: 0);
        }

        protected override void WriteRetrievedDocumentSize(long retrievedDocumentSize)
        {
            QueryMetricsTextWriter.AppendBytesToStringBuilder(stringBuilder, QueryMetricsTextWriter.RetrievedDocumentSize, retrievedDocumentSize, indentLevel: 0);
        }

        protected override void WriteOutputDocumentCount(long outputDocumentCount)
        {
            QueryMetricsTextWriter.AppendCountToStringBuilder(stringBuilder, QueryMetricsTextWriter.OutputDocumentCount, outputDocumentCount, indentLevel: 0);
        }

        protected override void WriteOutputDocumentSize(long outputDocumentSize)
        {
            QueryMetricsTextWriter.AppendBytesToStringBuilder(stringBuilder, QueryMetricsTextWriter.OutputDocumentSize, outputDocumentSize, indentLevel: 0);
        }

        protected override void WriteIndexHitRatio(double indexHitRatio)
        {
            QueryMetricsTextWriter.AppendPercentageToStringBuilder(stringBuilder, QueryMetricsTextWriter.IndexUtilization, indexHitRatio, indentLevel: 0);
        }

        protected override void WriteTotalQueryExecutionTime(TimeSpan totalQueryExecutionTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.TotalQueryExecutionTime, totalQueryExecutionTime, indentLevel: 0);
        }

        #region QueryPreparationTimes
        protected override void WriteBeforeQueryPreparationTimes()
        {
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, QueryMetricsTextWriter.QueryPreparationTimes, indentLevel: 1);
        }

        protected override void WriteQueryCompilationTime(TimeSpan queryCompilationTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.QueryCompileTime, queryCompilationTime, 2);
        }

        protected override void WriteLogicalPlanBuildTime(TimeSpan logicalPlanBuildTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.LogicalPlanBuildTime, logicalPlanBuildTime, 2);
        }

        protected override void WritePhysicalPlanBuildTime(TimeSpan physicalPlanBuildTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.PhysicalPlanBuildTime, physicalPlanBuildTime, 2);
        }

        protected override void WriteQueryOptimizationTime(TimeSpan queryOptimizationTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.QueryOptimizationTime, queryOptimizationTime, 2);
        }

        protected override void WriteAfterQueryPreparationTimes()
        {
            // Do Nothing
        }
        #endregion

        protected override void WriteIndexLookupTime(TimeSpan indexLookupTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.IndexLookupTime, indexLookupTime, indentLevel: 1);
        }

        protected override void WriteDocumentLoadTime(TimeSpan documentLoadTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.DocumentLoadTime, documentLoadTime, indentLevel: 1);
        }

        protected override void WriteVMExecutionTime(TimeSpan vmExecutionTime)
        {
            // Do Nothing
        }

        #region RuntimeExecutionTimes
        protected override void WriteBeforeRuntimeExecutionTimes()
        {
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, QueryMetricsTextWriter.RuntimeExecutionTimes, indentLevel: 1);
        }

        protected override void WriteQueryEngineExecutionTime(TimeSpan queryEngineExecutionTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.QueryEngineTimes, queryEngineExecutionTime, 2);
        }

        protected override void WriteSystemFunctionExecutionTime(TimeSpan systemFunctionExecutionTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.SystemFunctionExecuteTime, systemFunctionExecutionTime, 2);
        }

        protected override void WriteUserDefinedFunctionExecutionTime(TimeSpan userDefinedFunctionExecutionTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.UserDefinedFunctionExecutionTime, userDefinedFunctionExecutionTime, 2);
        }

        protected override void WriteAfterRuntimeExecutionTimes()
        {
            // Do Nothing
        }
        #endregion

        protected override void WriteDocumentWriteTime(TimeSpan documentWriteTime)
        {
            QueryMetricsTextWriter.AppendTimeSpanToStringBuilder(stringBuilder, QueryMetricsTextWriter.DocumentWriteTime, documentWriteTime, indentLevel: 1);
        }

        #region ClientSideMetrics
        protected override void WriteBeforeClientSideMetrics()
        {
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, QueryMetricsTextWriter.ClientSideQueryMetrics, indentLevel: 0);
        }

        protected override void WriteRetries(long retries)
        {
            QueryMetricsTextWriter.AppendCountToStringBuilder(stringBuilder, QueryMetricsTextWriter.Retries, retries, indentLevel: 1);
        }

        protected override void WriteRequestCharge(double requestCharge)
        {
            QueryMetricsTextWriter.AppendRUToStringBuilder(stringBuilder, QueryMetricsTextWriter.RequestCharge, requestCharge, indentLevel: 1);
        }

        protected override void WriteBeforePartitionExecutionTimeline()
        {
            QueryMetricsTextWriter.AppendNewlineToStringBuilder(stringBuilder);

            // Building the table for fetch execution ranges
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, QueryMetricsTextWriter.FetchExecutionRanges, indentLevel: 1);
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, PartitionExecutionTimelineTable.TopLine, indentLevel: 1);
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, PartitionExecutionTimelineTable.Header, indentLevel: 1);
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, PartitionExecutionTimelineTable.MiddleLine, indentLevel: 1);
        }

        protected override void WriteBeforeFetchExecutionRange()
        {
            // Do Nothing
        }

        protected override void WriteFetchPartitionKeyRangeId(string partitionId)
        {
            this.lastFetchPartitionId = partitionId;
        }

        protected override void WriteActivityId(string activityId)
        {
            this.lastActivityId = activityId;
        }

        protected override void WriteStartTime(DateTime startTime)
        {
            this.lastStartTime = startTime;
        }

        protected override void WriteEndTime(DateTime endTime)
        {
            this.lastEndTime = endTime;
        }

        protected override void WriteFetchDocumentCount(long numberOfDocuments)
        {
            this.lastFetchDocumentCount = numberOfDocuments;
        }

        protected override void WriteFetchRetryCount(long retryCount)
        {
            this.lastFetchRetryCount = retryCount;
        }

        protected override void WriteAfterFetchExecutionRange()
        {
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(
                stringBuilder,
                PartitionExecutionTimelineTable.GetRow(
                    this.lastFetchPartitionId,
                    this.lastActivityId,
                    this.lastStartTime.ToUniversalTime().ToString(DateTimeFormat),
                    this.lastEndTime.ToUniversalTime().ToString(DateTimeFormat),
                    (this.lastEndTime - this.lastStartTime).TotalMilliseconds.ToString("0.00"),
                    this.lastFetchDocumentCount,
                    this.lastFetchRetryCount),
                indentLevel: 1);
        }

        protected override void WriteAfterPartitionExecutionTimeline()
        {
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, PartitionExecutionTimelineTable.BottomLine, indentLevel: 1);
        }

        protected override void WriteBeforeSchedulingMetrics()
        {
            QueryMetricsTextWriter.AppendNewlineToStringBuilder(stringBuilder);

            // Building the table for scheduling metrics
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, QueryMetricsTextWriter.SchedulingMetrics, indentLevel: 1);
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, SchedulingMetricsTable.TopLine, indentLevel: 1);
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, SchedulingMetricsTable.Header, indentLevel: 1);
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, SchedulingMetricsTable.MiddleLine, indentLevel: 1);
        }

        protected override void WriteBeforePartitionSchedulingTimeSpan()
        {
            // Do Nothing
        }

        protected override void WritePartitionSchedulingTimeSpanId(string partitionId)
        {
            this.lastSchedulingPartitionId = partitionId;
        }

        protected override void WriteResponseTime(TimeSpan responseTime)
        {
            this.lastResponseTime = responseTime;
        }

        protected override void WriteRunTime(TimeSpan runTime)
        {
            this.lastRunTime = runTime;
        }

        protected override void WriteWaitTime(TimeSpan waitTime)
        {
            this.lastWaitTime = waitTime;
        }

        protected override void WriteTurnaroundTime(TimeSpan turnaroundTime)
        {
            this.lastTurnaroundTime = turnaroundTime;
        }

        protected override void WriteNumberOfPreemptions(long numPreemptions)
        {
            this.lastNumberOfPreemptions = numPreemptions;
        }

        protected override void WriteAfterPartitionSchedulingTimeSpan()
        {
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(
                stringBuilder,
                SchedulingMetricsTable.GetRow(
                    this.lastSchedulingPartitionId,
                    this.lastResponseTime.TotalMilliseconds.ToString("0.00"),
                    this.lastRunTime.TotalMilliseconds.ToString("0.00"),
                    this.lastWaitTime.TotalMilliseconds.ToString("0.00"),
                    this.lastTurnaroundTime.TotalMilliseconds.ToString("0.00"),
                    this.lastNumberOfPreemptions),
                indentLevel: 1);
        }

        protected override void WriteAfterSchedulingMetrics()
        {
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, SchedulingMetricsTable.BottomLine, indentLevel: 1);
        }

        protected override void WriteAfterClientSideMetrics()
        {
            // Do Nothing
        }
        #endregion

        protected override void WriteAfterQueryMetrics()
        {
            // Do Nothing
        }
        #region Helpers
        private static void AppendToStringBuilder(StringBuilder stringBuilder, string property, string value, string units, int indentLevel)
        {
            const string Indent = "  ";
            const string FormatString = "{0,-40} : {1,15} {2,-12}{3}";

            stringBuilder.AppendFormat(
                CultureInfo.InvariantCulture,
                FormatString,
                string.Concat(Enumerable.Repeat(Indent, indentLevel)) + property,
                value,
                units,
                Environment.NewLine);
        }

        private static void AppendBytesToStringBuilder(StringBuilder stringBuilder, string property, long bytes, int indentLevel)
        {
            const string BytesFormatString = "{0:n0}";
            const string BytesUnitString = "bytes";

            QueryMetricsTextWriter.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, BytesFormatString, bytes),
                BytesUnitString,
                indentLevel);
        }

        private static void AppendCountToStringBuilder(StringBuilder stringBuilder, string property, long count, int indentLevel)
        {
            const string CountFormatString = "{0:n0}";
            const string CountUnitString = "";

            QueryMetricsTextWriter.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, CountFormatString, count),
                CountUnitString,
                indentLevel);
        }

        private static void AppendPercentageToStringBuilder(StringBuilder stringBuilder, string property, double percentage, int indentLevel)
        {
            const string PercentageFormatString = "{0:n2}";
            const string PercentageUnitString = "%";

            QueryMetricsTextWriter.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, PercentageFormatString, percentage * 100),
                PercentageUnitString,
                indentLevel);
        }

        private static void AppendTimeSpanToStringBuilder(StringBuilder stringBuilder, string property, TimeSpan timeSpan, int indentLevel)
        {
            const string MillisecondsFormatString = "{0:n2}";
            const string MillisecondsUnitString = "milliseconds";

            QueryMetricsTextWriter.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, MillisecondsFormatString, timeSpan.TotalMilliseconds),
                MillisecondsUnitString,
                indentLevel);
        }

        private static void AppendHeaderToStringBuilder(StringBuilder stringBuilder, string headerTitle, int indentLevel)
        {
            const string Indent = "  ";
            const string FormatString = "{0}{1}";

            stringBuilder.AppendFormat(
                CultureInfo.InvariantCulture,
                FormatString,
                string.Concat(Enumerable.Repeat(Indent, indentLevel)) + headerTitle,
                Environment.NewLine);
        }

        private static void AppendRUToStringBuilder(StringBuilder stringBuilder, string property, double requestCharge, int indentLevel)
        {
            const string RequestChargeFormatString = "{0:n2}";
            const string RequestChargeUnitString = "RUs";

            QueryMetricsTextWriter.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, RequestChargeFormatString, requestCharge),
                RequestChargeUnitString,
                indentLevel);
        }

        private static void AppendNewlineToStringBuilder(StringBuilder stringBuilder)
        {
            QueryMetricsTextWriter.AppendHeaderToStringBuilder(stringBuilder, string.Empty, 0);
        }
        #endregion
    }
}
