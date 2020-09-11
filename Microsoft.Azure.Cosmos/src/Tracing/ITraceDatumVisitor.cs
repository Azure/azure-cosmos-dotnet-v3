// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal interface ITraceDatumVisitor
    {
        void Visit(CosmosDiagnosticsTraceDatum cosmosDiagnosticsTraceDatum);

        void Visit(QueryMetricsTraceDatum queryMetricsTraceDatum);
    }
}
