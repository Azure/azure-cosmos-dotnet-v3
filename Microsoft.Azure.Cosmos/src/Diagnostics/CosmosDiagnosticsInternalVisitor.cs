//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    internal abstract class CosmosDiagnosticsInternalVisitor
    {
        public abstract void Visit(PointOperationStatistics pointOperationStatistics);
        public abstract void Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext);
        public abstract void Visit(CosmosDiagnosticScope cosmosDiagnosticScope);
        public abstract void Visit(QueryPageDiagnostics queryPageDiagnostics);
        public abstract void Visit(AddressResolutionStatistics addressResolutionStatistics);
        public abstract void Visit(StoreResponseStatistics storeResponseStatistics);
        public abstract void Visit(CosmosClientSideRequestStatistics clientSideRequestStatistics);
        public abstract void Visit(FeedRangeStatistics feedRangeStatistics);
    }
}
