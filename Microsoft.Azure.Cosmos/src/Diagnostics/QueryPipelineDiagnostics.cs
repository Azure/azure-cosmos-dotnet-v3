//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;

    internal abstract class QueryPipelineDiagnostics
    {
        internal abstract IDisposable CreateScope(string name);
    }
}
