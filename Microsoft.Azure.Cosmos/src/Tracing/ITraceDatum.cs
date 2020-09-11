// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    internal interface ITraceDatum
    {
        void Accept(ITraceDatumVisitor traceDatumVisitor);
    }
}
