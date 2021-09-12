// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    /// <summary>
    /// Visitor for <see cref="TraceDatum"/>
    /// </summary>
    internal interface ITraceDatumVisitor
    {
        void Visit(QueryMetricsTraceDatum queryMetricsTraceDatum);
        void Visit(PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum);
        void Visit(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum);
        void Visit(CpuHistoryTraceDatum cpuHistoryTraceDatum);
        void Visit(ClientConfigurationTraceDatum clientConfigurationTraceDatum);
    }
}
