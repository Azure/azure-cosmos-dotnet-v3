//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Linq;

    /// <summary>
    /// Base class for visiting and serializing a <see cref="QueryMetrics"/>.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    abstract class QueryMetricsWriter
    {
        public void WriteQueryMetrics(QueryMetrics queryMetrics)
        {
            this.WriteBeforeQueryMetrics();

            // Top Level Properties
            this.WriteRetrievedDocumentCount(queryMetrics.BackendMetrics.RetrievedDocumentCount);
            this.WriteRetrievedDocumentSize(queryMetrics.BackendMetrics.RetrievedDocumentSize);
            this.WriteOutputDocumentCount(queryMetrics.BackendMetrics.OutputDocumentCount);
            this.WriteOutputDocumentSize(queryMetrics.BackendMetrics.OutputDocumentSize);
            this.WriteIndexHitRatio(queryMetrics.BackendMetrics.IndexHitRatio);

            this.WriteTotalQueryExecutionTime(queryMetrics.BackendMetrics.TotalTime);

            // QueryPreparationTimes
            this.WriteQueryPreparationTimes(queryMetrics.BackendMetrics.QueryPreparationTimes);

            this.WriteIndexLookupTime(queryMetrics.BackendMetrics.IndexLookupTime);
            this.WriteDocumentLoadTime(queryMetrics.BackendMetrics.DocumentLoadTime);
            this.WriteVMExecutionTime(queryMetrics.BackendMetrics.VMExecutionTime);

            // RuntimesExecutionTimes
            this.WriteRuntimesExecutionTimes(queryMetrics.BackendMetrics.RuntimeExecutionTimes);

            this.WriteDocumentWriteTime(queryMetrics.BackendMetrics.DocumentWriteTime);
            this.WriteRequestCharge(queryMetrics.ClientSideMetrics.RequestCharge);

#if false
            // ClientSideMetrics
            this.WriteClientSideMetrics(queryMetrics.ClientSideMetrics);

            // IndexUtilizationInfo
            this.WriteBeforeIndexUtilizationInfo();

            this.WriteIndexUtilizationInfo(queryMetrics.IndexUtilizationInfo);
#endif
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

#region IndexUtilizationInfo

        protected abstract void WriteBeforeIndexUtilizationInfo();

        protected abstract void WriteIndexUtilizationInfo(IndexUtilizationInfo indexUtilizationInfo);

        protected abstract void WriteAfterIndexUtilizationInfo();
#endregion

        protected abstract void WriteAfterQueryMetrics();
    }
}
