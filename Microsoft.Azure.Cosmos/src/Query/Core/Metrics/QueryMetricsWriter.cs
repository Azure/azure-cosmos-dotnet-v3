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
            this.WriteRetrievedDocumentCount(queryMetrics.ServerSideMetrics.RetrievedDocumentCount);
            this.WriteRetrievedDocumentSize(queryMetrics.ServerSideMetrics.RetrievedDocumentSize);
            this.WriteOutputDocumentCount(queryMetrics.ServerSideMetrics.OutputDocumentCount);
            this.WriteOutputDocumentSize(queryMetrics.ServerSideMetrics.OutputDocumentSize);
            this.WriteIndexHitRatio(queryMetrics.ServerSideMetrics.IndexHitRatio);

            this.WriteTotalQueryExecutionTime(queryMetrics.ServerSideMetrics.TotalTime);

            // QueryPreparationTimes
            this.WriteQueryPreparationTime(queryMetrics.ServerSideMetrics.QueryPreparationTimes);

            this.WriteIndexLookupTime(queryMetrics.ServerSideMetrics.IndexLookupTime);
            this.WriteDocumentLoadTime(queryMetrics.ServerSideMetrics.DocumentLoadTime);
            this.WriteVMExecutionTime(queryMetrics.ServerSideMetrics.VMExecutionTime);

            // RuntimesExecutionTimes
            this.WriteRuntimeExecutionTime(queryMetrics.ServerSideMetrics.RuntimeExecutionTimes);

            this.WriteDocumentWriteTime(queryMetrics.ServerSideMetrics.DocumentWriteTime);
#if false
            // ClientSideMetrics
            this.WriteClientSideMetrics(queryMetrics.ClientSideMetrics);
#endif
            // IndexUtilizationInfo
            this.WriteBeforeIndexUtilizationInfo();

            this.WriteIndexUtilizationInfo(queryMetrics.IndexUtilizationInfo);
            
            this.WriteAfterIndexUtilizationInfo();

            this.WriteAfterQueryMetrics();
        }

        protected abstract void WriteBeforeQueryMetrics();

        protected abstract void WriteRetrievedDocumentCount(long retrievedDocumentCount);

        protected abstract void WriteRetrievedDocumentSize(long retrievedDocumentSize);

        protected abstract void WriteOutputDocumentCount(long outputDocumentCount);

        protected abstract void WriteOutputDocumentSize(long outputDocumentSize);

        protected abstract void WriteIndexHitRatio(double indexHitRatio);

        protected abstract void WriteTotalQueryExecutionTime(TimeSpan totalQueryExecutionTime);

        protected abstract void WriteQueryPreparationTime(QueryPreparationTimesInternal queryPreparationTimes);

        protected abstract void WriteIndexLookupTime(TimeSpan indexLookupTime);

        protected abstract void WriteDocumentLoadTime(TimeSpan documentLoadTime);

        protected abstract void WriteVMExecutionTime(TimeSpan vMExecutionTime);

        protected abstract void WriteRuntimeExecutionTime(RuntimeExecutionTimesInternal runtimeExecutionTimes);

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
