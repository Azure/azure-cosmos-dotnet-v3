//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    internal abstract class CosmosDiagnosticsInternalVisitor<TResult>
    {
        public abstract TResult Visit(CosmosDiagnosticsAggregate cosmosDiagnosticsAggregate);

        public abstract TResult Visit(PointOperationStatistics pointOperationStatistics);

        public abstract TResult Visit(QueryAggregateDiagnostics queryAggregateDiagnostics);
    }
}
