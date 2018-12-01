//-----------------------------------------------------------------------
// <copyright file="QueryMetricsConstants.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

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

        // QueryMetrics Text
        public const string ActivityIds = "Activity Ids";
        public const string RetrievedDocumentCountText = "Retrieved Document Count";
        public const string RetrievedDocumentSizeText = "Retrieved Document Size";
        public const string OutputDocumentCountText = "Output Document Count";
        public const string OutputDocumentSizeText = "Output Document Size";
        public const string IndexUtilizationText = "Index Utilization";
        public const string TotalQueryExecutionTimeText = "Total Query Execution Time";

        // QueryPreparationTimes Text
        public const string QueryPreparationTimesText = "Query Preparation Times";
        public const string QueryCompileTimeText = "Query Compilation Time";
        public const string LogicalPlanBuildTimeText = "Logical Plan Build Time";
        public const string PhysicalPlanBuildTimeText = "Physical Plan Build Time";
        public const string QueryOptimizationTimeText = "Query Optimization Time";

        // QueryTimes Text
        public const string QueryEngineTimesText = "Query Engine Times";
        public const string IndexLookupTimeText = "Index Lookup Time";
        public const string DocumentLoadTimeText = "Document Load Time";
        public const string WriteOutputTimeText = "Document Write Time";

        // RuntimeExecutionTimes Text
        public const string RuntimeExecutionTimesText = "Runtime Execution Times";
        public const string TotalExecutionTimeText = "Query Engine Execution Time";
        public const string SystemFunctionExecuteTimeText = "System Function Execution Time";
        public const string UserDefinedFunctionExecutionTimeText = "User-defined Function Execution Time";

        // ClientSideQueryMetrics Text
        public const string ClientSideQueryMetricsText = "Client Side Metrics";
        public const string RetriesText = "Retry Count";
        public const string RequestChargeText = "Request Charge";
        public const string FetchExecutionRangesText = "Partition Execution Timeline";
        public const string SchedulingMetricsText = "Scheduling Metrics";
    }
}
