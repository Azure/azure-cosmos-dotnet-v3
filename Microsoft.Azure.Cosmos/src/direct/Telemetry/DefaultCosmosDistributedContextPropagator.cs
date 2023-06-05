//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NETSTANDARD2_0_OR_GREATER
using Microsoft.Azure.Documents.Collections;
using System;
using System.Diagnostics;

namespace Microsoft.Azure.Documents.Telemetry
{
    internal class DefaultCosmosDistributedContextPropagator : CosmosDistributedContextPropagatorBase
    {
        internal override void Inject(Activity activity, INameValueCollection headers)
        {
            if (activity == null || headers == null)
            {
                return;
            }

            headers.Set(HttpConstants.HttpHeaders.TraceParent, activity.Id);
            headers.Set(HttpConstants.HttpHeaders.TraceState, activity.TraceStateString);
        }
    }
}
#endif