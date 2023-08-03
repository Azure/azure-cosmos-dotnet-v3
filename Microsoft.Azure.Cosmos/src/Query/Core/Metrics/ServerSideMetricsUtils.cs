//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    internal static class ServerSideMetricsUtils
    {
        public static string FormatTrace(this ServerSideMetricsInternal serverSideMetrics)
        {
            return $"totalExecutionTimeInMs={serverSideMetrics.TotalTime.TotalMilliseconds};queryCompileTimeInMs={serverSideMetrics.QueryPreparationTimes.QueryCompilationTime.TotalMilliseconds};queryLogicalPlanBuildTimeInMs={serverSideMetrics.QueryPreparationTimes.LogicalPlanBuildTime.TotalMilliseconds};queryPhysicalPlanBuildTimeInMs={serverSideMetrics.QueryPreparationTimes.PhysicalPlanBuildTime.TotalMilliseconds};queryOptimizationTimeInMs={serverSideMetrics.QueryPreparationTimes.QueryOptimizationTime.TotalMilliseconds};indexLookupTimeInMs={serverSideMetrics.IndexLookupTime.TotalMilliseconds};documentLoadTimeInMs={serverSideMetrics.DocumentLoadTime.TotalMilliseconds};systemFunctionExecuteTimeInMs={serverSideMetrics.RuntimeExecutionTimes.SystemFunctionExecutionTime.TotalMilliseconds};userFunctionExecuteTimeInMs={serverSideMetrics.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime.TotalMilliseconds};retrievedDocumentCount={serverSideMetrics.RetrievedDocumentCount};retrievedDocumentSize={serverSideMetrics.RetrievedDocumentSize};outputDocumentCount={serverSideMetrics.OutputDocumentCount};outputDocumentSize={serverSideMetrics.OutputDocumentSize};writeOutputTimeInMs={serverSideMetrics.DocumentWriteTime.TotalMilliseconds};indexUtilizationRatio={serverSideMetrics.IndexHitRatio}";
        }

        public static string FormatTrace(this ServerSideMetrics serverSideMetrics)
        {
            return $"totalExecutionTimeInMs={serverSideMetrics.TotalTime.TotalMilliseconds};queryCompileTimeInMs={serverSideMetrics.QueryPreparationTimes.QueryCompilationTime.TotalMilliseconds};queryLogicalPlanBuildTimeInMs={serverSideMetrics.QueryPreparationTimes.LogicalPlanBuildTime.TotalMilliseconds};queryPhysicalPlanBuildTimeInMs={serverSideMetrics.QueryPreparationTimes.PhysicalPlanBuildTime.TotalMilliseconds};queryOptimizationTimeInMs={serverSideMetrics.QueryPreparationTimes.QueryOptimizationTime.TotalMilliseconds};indexLookupTimeInMs={serverSideMetrics.IndexLookupTime.TotalMilliseconds};documentLoadTimeInMs={serverSideMetrics.DocumentLoadTime.TotalMilliseconds};systemFunctionExecuteTimeInMs={serverSideMetrics.RuntimeExecutionTimes.SystemFunctionExecutionTime.TotalMilliseconds};userFunctionExecuteTimeInMs={serverSideMetrics.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime.TotalMilliseconds};retrievedDocumentCount={serverSideMetrics.RetrievedDocumentCount};retrievedDocumentSize={serverSideMetrics.RetrievedDocumentSize};outputDocumentCount={serverSideMetrics.OutputDocumentCount};outputDocumentSize={serverSideMetrics.OutputDocumentSize};writeOutputTimeInMs={serverSideMetrics.DocumentWriteTime.TotalMilliseconds};indexUtilizationRatio={serverSideMetrics.IndexHitRatio}";
        }
    }
}
