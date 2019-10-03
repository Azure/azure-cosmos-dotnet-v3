//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text;

    internal sealed class QueryMetricsDelimitedStringWriter : QueryMetricsWriter
    {
        #region Constants
        // QueryMetrics
        private const string RetrievedDocumentCount = "retrievedDocumentCount";
        private const string RetrievedDocumentSize = "retrievedDocumentSize";
        private const string OutputDocumentCount = "outputDocumentCount";
        private const string OutputDocumentSize = "outputDocumentSize";
        private const string IndexHitRatio = "indexUtilizationRatio";
        private const string IndexHitDocumentCount = "indexHitDocumentCount";
        private const string TotalQueryExecutionTimeInMs = "totalExecutionTimeInMs";

        // QueryPreparationTimes
        private const string QueryCompileTimeInMs = "queryCompileTimeInMs";
        private const string LogicalPlanBuildTimeInMs = "queryLogicalPlanBuildTimeInMs";
        private const string PhysicalPlanBuildTimeInMs = "queryPhysicalPlanBuildTimeInMs";
        private const string QueryOptimizationTimeInMs = "queryOptimizationTimeInMs";

        // QueryTimes
        private const string IndexLookupTimeInMs = "indexLookupTimeInMs";
        private const string DocumentLoadTimeInMs = "documentLoadTimeInMs";
        private const string VMExecutionTimeInMs = "VMExecutionTimeInMs";
        private const string DocumentWriteTimeInMs = "writeOutputTimeInMs";

        // RuntimeExecutionTimes
        private const string QueryEngineTimes = "queryEngineTimes";
        private const string SystemFunctionExecuteTimeInMs = "systemFunctionExecuteTimeInMs";
        private const string UserDefinedFunctionExecutionTimeInMs = "userFunctionExecuteTimeInMs";

        private const string KeyValueDelimiter = "=";
        private const string KeyValuePairDelimiter = ";";
        #endregion

        private readonly StringBuilder stringBuilder;

        public QueryMetricsDelimitedStringWriter(StringBuilder stringBuilder)
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
            this.AppendKeyValuePair(QueryMetricsDelimitedStringWriter.RetrievedDocumentCount, retrievedDocumentCount);
        }

        protected override void WriteRetrievedDocumentSize(long retrievedDocumentSize)
        {
            this.AppendKeyValuePair(QueryMetricsDelimitedStringWriter.RetrievedDocumentSize, retrievedDocumentSize);
        }

        protected override void WriteOutputDocumentCount(long outputDocumentCount)
        {
            this.AppendKeyValuePair(QueryMetricsDelimitedStringWriter.OutputDocumentCount, outputDocumentCount);
        }

        protected override void WriteOutputDocumentSize(long outputDocumentSize)
        {
            this.AppendKeyValuePair(QueryMetricsDelimitedStringWriter.OutputDocumentSize, outputDocumentSize);
        }

        protected override void WriteIndexHitRatio(double indexHitRatio)
        {
            this.AppendKeyValuePair(QueryMetricsDelimitedStringWriter.IndexHitRatio, indexHitRatio);
        }

        protected override void WriteTotalQueryExecutionTime(TimeSpan totalQueryExecutionTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.TotalQueryExecutionTimeInMs, totalQueryExecutionTime);
        }

        #region QueryPreparationTimes
        protected override void WriteBeforeQueryPreparationTimes()
        {
            // Do Nothing
        }

        protected override void WriteQueryCompilationTime(TimeSpan queryCompilationTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.QueryCompileTimeInMs, queryCompilationTime);
        }

        protected override void WriteLogicalPlanBuildTime(TimeSpan logicalPlanBuildTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.LogicalPlanBuildTimeInMs, logicalPlanBuildTime);
        }

        protected override void WritePhysicalPlanBuildTime(TimeSpan physicalPlanBuildTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.PhysicalPlanBuildTimeInMs, physicalPlanBuildTime);
        }

        protected override void WriteQueryOptimizationTime(TimeSpan queryOptimizationTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.QueryOptimizationTimeInMs, queryOptimizationTime);
        }

        protected override void WriteAfterQueryPreparationTimes()
        {
            // Do Nothing
        }
        #endregion

        protected override void WriteIndexLookupTime(TimeSpan indexLookupTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.IndexLookupTimeInMs, indexLookupTime);
        }

        protected override void WriteDocumentLoadTime(TimeSpan documentLoadTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.DocumentLoadTimeInMs, documentLoadTime);
        }

        protected override void WriteVMExecutionTime(TimeSpan vmExecutionTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.VMExecutionTimeInMs, vmExecutionTime);
        }

        #region RuntimeExecutionTimes
        protected override void WriteBeforeRuntimeExecutionTimes()
        {
            // Do Nothing
        }

        protected override void WriteQueryEngineExecutionTime(TimeSpan queryEngineExecutionTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.QueryEngineTimes, queryEngineExecutionTime);
        }

        protected override void WriteSystemFunctionExecutionTime(TimeSpan systemFunctionExecutionTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.SystemFunctionExecuteTimeInMs, systemFunctionExecutionTime);
        }

        protected override void WriteUserDefinedFunctionExecutionTime(TimeSpan userDefinedFunctionExecutionTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.UserDefinedFunctionExecutionTimeInMs, userDefinedFunctionExecutionTime);
        }

        protected override void WriteAfterRuntimeExecutionTimes()
        {
            // Do Nothing
        }
        #endregion

        protected override void WriteDocumentWriteTime(TimeSpan documentWriteTime)
        {
            this.AppendTimeSpan(QueryMetricsDelimitedStringWriter.DocumentWriteTimeInMs, documentWriteTime);
        }

        #region ClientSideMetrics
        protected override void WriteBeforeClientSideMetrics()
        {
            // Do Nothing
        }

        protected override void WriteRetries(long retries)
        {
            // Do Nothing
        }

        protected override void WriteRequestCharge(double requestCharge)
        {
            // Do Nothing
        }

        protected override void WriteBeforePartitionExecutionTimeline()
        {
            // Do Nothing
        }

        protected override void WriteBeforeFetchExecutionRange()
        {
            // Do Nothing
        }

        protected override void WriteFetchPartitionKeyRangeId(string partitionId)
        {
            // Do Nothing
        }

        protected override void WriteActivityId(string activityId)
        {
            // Do Nothing
        }

        protected override void WriteStartTime(DateTime startTime)
        {
            // Do Nothing
        }

        protected override void WriteEndTime(DateTime endTime)
        {
            // Do Nothing
        }

        protected override void WriteFetchDocumentCount(long numberOfDocuments)
        {
            // Do Nothing
        }

        protected override void WriteFetchRetryCount(long retryCount)
        {
            // Do Nothing
        }

        protected override void WriteAfterFetchExecutionRange()
        {
            // Do Nothing
        }

        protected override void WriteAfterPartitionExecutionTimeline()
        {
            // Do Nothing
        }

        protected override void WriteBeforeSchedulingMetrics()
        {
            // Do Nothing
        }

        protected override void WriteBeforePartitionSchedulingTimeSpan()
        {
            // Do Nothing
        }

        protected override void WritePartitionSchedulingTimeSpanId(string partitionId)
        {
            // Do Nothing
        }

        protected override void WriteResponseTime(TimeSpan responseTime)
        {
            // Do Nothing
        }

        protected override void WriteRunTime(TimeSpan runTime)
        {
            // Do Nothing
        }

        protected override void WriteWaitTime(TimeSpan waitTime)
        {
            // Do Nothing
        }

        protected override void WriteTurnaroundTime(TimeSpan turnaroundTime)
        {
            // Do Nothing
        }

        protected override void WriteNumberOfPreemptions(long numPreemptions)
        {
            // Do Nothing
        }

        protected override void WriteAfterPartitionSchedulingTimeSpan()
        {
            // Do Nothing
        }

        protected override void WriteAfterSchedulingMetrics()
        {
            // Do Nothing
        }

        protected override void WriteAfterClientSideMetrics()
        {
            // Do Nothing
        }
        #endregion

        protected override void WriteAfterQueryMetrics()
        {
            // Remove last ";" symbol
            this.stringBuilder.Length--;
        }

        private void AppendKeyValuePair<T>(string name, T value)
        {
            this.stringBuilder.Append(name);
            this.stringBuilder.Append(KeyValueDelimiter);
            this.stringBuilder.Append(value);
            this.stringBuilder.Append(KeyValuePairDelimiter);
        }

        private void AppendTimeSpan(string name, TimeSpan dateTime)
        {
            this.AppendKeyValuePair(name, dateTime.TotalMilliseconds.ToString("0.00"));
        }
    }
}
