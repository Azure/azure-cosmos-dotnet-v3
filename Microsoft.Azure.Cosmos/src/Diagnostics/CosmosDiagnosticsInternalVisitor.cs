//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    internal abstract class CosmosDiagnosticsInternalVisitor
    {
        public abstract void Visit(PointOperationStatistics pointOperationStatistics);
        public abstract void Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext);
    }
}
