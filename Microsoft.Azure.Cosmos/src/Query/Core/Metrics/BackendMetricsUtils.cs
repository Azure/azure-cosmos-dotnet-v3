//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    internal static class BackendMetricsUtils
    {
        public static string FormatTrace(this BackendMetrics backendMetrics)
        {
            return $"totalExecutionTimeInMs={backendMetrics.TotalTime.TotalMilliseconds};queryCompileTimeInMs={backendMetrics.QueryPreparationTimes.QueryCompilationTime.TotalMilliseconds};queryLogicalPlanBuildTimeInMs={backendMetrics.QueryPreparationTimes.LogicalPlanBuildTime.TotalMilliseconds};queryPhysicalPlanBuildTimeInMs={backendMetrics.QueryPreparationTimes.PhysicalPlanBuildTime.TotalMilliseconds};queryOptimizationTimeInMs={backendMetrics.QueryPreparationTimes.QueryOptimizationTime.TotalMilliseconds};indexLookupTimeInMs={backendMetrics.IndexLookupTime.TotalMilliseconds};documentLoadTimeInMs={backendMetrics.DocumentLoadTime.TotalMilliseconds};systemFunctionExecuteTimeInMs={backendMetrics.RuntimeExecutionTimes.SystemFunctionExecutionTime.TotalMilliseconds};userFunctionExecuteTimeInMs={backendMetrics.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime.TotalMilliseconds};retrievedDocumentCount={backendMetrics.RetrievedDocumentCount};retrievedDocumentSize={backendMetrics.RetrievedDocumentSize};outputDocumentCount={backendMetrics.OutputDocumentCount};outputDocumentSize={backendMetrics.OutputDocumentSize};writeOutputTimeInMs={backendMetrics.DocumentWriteTime.TotalMilliseconds};indexUtilizationRatio={backendMetrics.IndexHitRatio}";
        }
    }
}
