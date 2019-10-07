//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Linq;

    internal abstract class QueryMetricsWriter
    {
        public void WriteQueryMetrics(QueryMetrics queryMetrics)
        {
            this.WriteBeforeQueryMetrics();

            // Top Level Properties
            this.WriteRetrievedDocumentCount(queryMetrics.RetrievedDocumentCount);
            this.WriteRetrievedDocumentSize(queryMetrics.RetrievedDocumentSize);
            this.WriteOutputDocumentCount(queryMetrics.OutputDocumentCount);
            this.WriteOutputDocumentSize(queryMetrics.OutputDocumentSize);
            this.WriteIndexHitRatio(queryMetrics.IndexHitRatio);
            this.WriteTotalQueryExecutionTime(queryMetrics.TotalQueryExecutionTime);

            // QueryPreparationTimes
            this.WriteQueryPreparationTimes(queryMetrics.QueryPreparationTimes);

            this.WriteIndexLookupTime(queryMetrics.IndexLookupTime);
            this.WriteDocumentLoadTime(queryMetrics.DocumentLoadTime);
            this.WriteVMExecutionTime(queryMetrics.VMExecutionTime);

            // RuntimesExecutionTimes
            this.WriteRuntimesExecutionTimes(queryMetrics.RuntimeExecutionTimes);

            this.WriteDocumentWriteTime(queryMetrics.DocumentWriteTime);

            // ClientSideMetrics
            this.WriteClientSideMetrics(queryMetrics.ClientSideMetrics);

            this.WriteAfterQueryMetrics();
        }

        protected abstract void WriteBeforeQueryMetrics();

        protected abstract void WriteRetrievedDocumentCount(long retrievedDocumentCount);

        protected abstract void WriteRetrievedDocumentSize(long retrievedDocumentSize);

        protected abstract void WriteOutputDocumentCount(long outputDocumentCount);

        protected abstract void WriteOutputDocumentSize(long outputDocumentSize);

        protected abstract void WriteIndexHitRatio(double indexHitRatio);

        protected abstract void WriteTotalQueryExecutionTime(TimeSpan totalQueryExecutionTime);

        #region QueryPreparationTimes
        private void WriteQueryPreparationTimes(QueryPreparationTimes queryPreparationTimes)
        {
            this.WriteBeforeQueryPreparationTimes();

            this.WriteQueryCompilationTime(queryPreparationTimes.QueryCompilationTime);
            this.WriteLogicalPlanBuildTime(queryPreparationTimes.LogicalPlanBuildTime);
            this.WritePhysicalPlanBuildTime(queryPreparationTimes.PhysicalPlanBuildTime);
            this.WriteQueryOptimizationTime(queryPreparationTimes.QueryOptimizationTime);

            this.WriteAfterQueryPreparationTimes();
        }

        protected abstract void WriteBeforeQueryPreparationTimes();

        protected abstract void WriteQueryCompilationTime(TimeSpan queryCompilationTime);

        protected abstract void WriteLogicalPlanBuildTime(TimeSpan logicalPlanBuildTime);

        protected abstract void WritePhysicalPlanBuildTime(TimeSpan physicalPlanBuildTime);

        protected abstract void WriteQueryOptimizationTime(TimeSpan queryOptimizationTime);

        protected abstract void WriteAfterQueryPreparationTimes();
        #endregion

        protected abstract void WriteIndexLookupTime(TimeSpan indexLookupTime);

        protected abstract void WriteDocumentLoadTime(TimeSpan documentLoadTime);

        protected abstract void WriteVMExecutionTime(TimeSpan vMExecutionTime);

        #region RuntimeExecutionTimes
        private void WriteRuntimesExecutionTimes(RuntimeExecutionTimes runtimeExecutionTimes)
        {
            this.WriteBeforeRuntimeExecutionTimes();

            this.WriteQueryEngineExecutionTime(runtimeExecutionTimes.QueryEngineExecutionTime);
            this.WriteSystemFunctionExecutionTime(runtimeExecutionTimes.SystemFunctionExecutionTime);
            this.WriteUserDefinedFunctionExecutionTime(runtimeExecutionTimes.UserDefinedFunctionExecutionTime);

            this.WriteAfterRuntimeExecutionTimes();
        }

        protected abstract void WriteBeforeRuntimeExecutionTimes();

        protected abstract void WriteQueryEngineExecutionTime(TimeSpan queryEngineExecutionTime);

        protected abstract void WriteSystemFunctionExecutionTime(TimeSpan systemFunctionExecutionTime);

        protected abstract void WriteUserDefinedFunctionExecutionTime(TimeSpan userDefinedFunctionExecutionTime);

        protected abstract void WriteAfterRuntimeExecutionTimes();
        #endregion

        protected abstract void WriteDocumentWriteTime(TimeSpan documentWriteTime);

        #region ClientSideMetrics
        private void WriteClientSideMetrics(ClientSideMetrics clientSideMetrics)
        {
            this.WriteBeforeClientSideMetrics();

            this.WriteRetries(clientSideMetrics.Retries);
            this.WriteRequestCharge(clientSideMetrics.RequestCharge);
            this.WritePartitionExecutionTimeline(clientSideMetrics);
            this.WriteSchedulingMetrics(clientSideMetrics);

            this.WriteAfterClientSideMetrics();
        }

        protected abstract void WriteBeforeClientSideMetrics();

        protected abstract void WriteRetries(long retries);

        protected abstract void WriteRequestCharge(double requestCharge);

        private void WritePartitionExecutionTimeline(ClientSideMetrics clientSideMetrics)
        {
            this.WriteBeforePartitionExecutionTimeline();

            foreach (FetchExecutionRange fetchExecutionRange in clientSideMetrics.FetchExecutionRanges.OrderBy(fetchExecutionRange => fetchExecutionRange.StartTime))
            {
                this.WriteFetchExecutionRange(fetchExecutionRange);
            }

            this.WriteAfterPartitionExecutionTimeline();
        }

        protected abstract void WriteBeforePartitionExecutionTimeline();

        private void WriteFetchExecutionRange(FetchExecutionRange fetchExecutionRange)
        {
            this.WriteBeforeFetchExecutionRange();

            this.WriteFetchPartitionKeyRangeId(fetchExecutionRange.PartitionId);
            this.WriteActivityId(fetchExecutionRange.ActivityId);
            this.WriteStartTime(fetchExecutionRange.StartTime);
            this.WriteEndTime(fetchExecutionRange.EndTime);
            this.WriteFetchDocumentCount(fetchExecutionRange.NumberOfDocuments);
            this.WriteFetchRetryCount(fetchExecutionRange.RetryCount);

            this.WriteAfterFetchExecutionRange();
        }

        protected abstract void WriteBeforeFetchExecutionRange();

        protected abstract void WriteFetchPartitionKeyRangeId(string partitionId);

        protected abstract void WriteActivityId(string activityId);

        protected abstract void WriteStartTime(DateTime startTime);

        protected abstract void WriteEndTime(DateTime endTime);

        protected abstract void WriteFetchDocumentCount(long numberOfDocuments);

        protected abstract void WriteFetchRetryCount(long retryCount);

        protected abstract void WriteAfterFetchExecutionRange();

        protected abstract void WriteAfterPartitionExecutionTimeline();

        private void WriteSchedulingMetrics(ClientSideMetrics clientSideMetrics)
        {
            this.WriteBeforeSchedulingMetrics();

            foreach (Tuple<string, SchedulingTimeSpan> partitionSchedulingTimeSpan in clientSideMetrics.PartitionSchedulingTimeSpans.OrderBy(x => x.Item2.ResponseTime))
            {
                string partitionId = partitionSchedulingTimeSpan.Item1;
                SchedulingTimeSpan schedulingTimeSpan = partitionSchedulingTimeSpan.Item2;

                this.WritePartitionSchedulingTimeSpan(partitionId, schedulingTimeSpan);
            }

            this.WriteAfterSchedulingMetrics();
        }

        protected abstract void WriteBeforeSchedulingMetrics();

        private void WritePartitionSchedulingTimeSpan(string partitionId, SchedulingTimeSpan schedulingTimeSpan)
        {
            this.WriteBeforePartitionSchedulingTimeSpan();

            this.WritePartitionSchedulingTimeSpanId(partitionId);
            this.WriteResponseTime(schedulingTimeSpan.ResponseTime);
            this.WriteRunTime(schedulingTimeSpan.RunTime);
            this.WriteWaitTime(schedulingTimeSpan.WaitTime);
            this.WriteTurnaroundTime(schedulingTimeSpan.TurnaroundTime);
            this.WriteNumberOfPreemptions(schedulingTimeSpan.NumPreemptions);

            this.WriteAfterPartitionSchedulingTimeSpan();
        }

        protected abstract void WriteBeforePartitionSchedulingTimeSpan();

        protected abstract void WritePartitionSchedulingTimeSpanId(string partitionId);

        protected abstract void WriteResponseTime(TimeSpan responseTime);

        protected abstract void WriteRunTime(TimeSpan runTime);

        protected abstract void WriteWaitTime(TimeSpan waitTime);

        protected abstract void WriteTurnaroundTime(TimeSpan turnaroundTime);

        protected abstract void WriteNumberOfPreemptions(long numPreemptions);

        protected abstract void WriteAfterPartitionSchedulingTimeSpan();

        protected abstract void WriteAfterSchedulingMetrics();

        protected abstract void WriteAfterClientSideMetrics();
        #endregion

        protected abstract void WriteAfterQueryMetrics();
    }
}
