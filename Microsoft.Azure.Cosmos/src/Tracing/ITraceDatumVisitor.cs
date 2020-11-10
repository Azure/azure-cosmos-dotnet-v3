// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    /// <summary>
    /// Visitor for <see cref="ITraceDatum"/>
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
        interface ITraceDatumVisitor
    {
        /// <summary>
        /// Visits a <see cref="CosmosDiagnosticsTraceDatum"/> instance.
        /// </summary>
        /// <param name="cosmosDiagnosticsTraceDatum">The datum to visit.</param>
        void Visit(CosmosDiagnosticsTraceDatum cosmosDiagnosticsTraceDatum);

        /// <summary>
        /// Visits a <see cref="QueryMetricsTraceDatum"/> instance.
        /// </summary>
        /// <param name="queryMetricsTraceDatum">The datum to visit.</param>
        void Visit(QueryMetricsTraceDatum queryMetricsTraceDatum);
    }
}
