//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NETSTANDARD2_0_OR_GREATER
using Microsoft.Azure.Documents.Collections;
using System;
using System.Diagnostics;

namespace Microsoft.Azure.Documents.Telemetry
{
    internal abstract class CosmosDistributedContextPropagatorBase
    {
        internal abstract void Inject(Activity activity, INameValueCollection headers);
    }
}
#endif