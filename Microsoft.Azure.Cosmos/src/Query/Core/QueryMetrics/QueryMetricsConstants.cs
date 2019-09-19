//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    internal static class QueryMetricsConstants
    {
        // QueryMetrics
        public const string RetrievedDocumentCount = "retrievedDocumentCount";
        public const string RetrievedDocumentSize = "retrievedDocumentSize";
        public const string OutputDocumentCount = "outputDocumentCount";
        public const string OutputDocumentSize = "outputDocumentSize";
        public const string IndexHitRatio = "indexUtilizationRatio";
        public const string IndexHitDocumentCount = "indexHitDocumentCount";
        public const string TotalQueryExecutionTimeInMs = "totalExecutionTimeInMs";

        // QueryPreparationTimes
        public const string QueryCompileTimeInMs = "queryCompileTimeInMs";
        public const string LogicalPlanBuildTimeInMs = "queryLogicalPlanBuildTimeInMs";
        public const string PhysicalPlanBuildTimeInMs = "queryPhysicalPlanBuildTimeInMs";
        public const string QueryOptimizationTimeInMs = "queryOptimizationTimeInMs";

        // QueryTimes
        public const string IndexLookupTimeInMs = "indexLookupTimeInMs";
        public const string DocumentLoadTimeInMs = "documentLoadTimeInMs";
        public const string VMExecutionTimeInMs = "VMExecutionTimeInMs";
        public const string DocumentWriteTimeInMs = "writeOutputTimeInMs";

        // RuntimeExecutionTimes
        public const string QueryEngineTimes = "queryEngineTimes";
        public const string SystemFunctionExecuteTimeInMs = "systemFunctionExecuteTimeInMs";
        public const string UserDefinedFunctionExecutionTimeInMs = "userFunctionExecuteTimeInMs";
    }
}
