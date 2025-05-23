//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP2_0_OR_GREATER
using Microsoft.Azure.Documents.Collections;
using System;
using System.Diagnostics;

namespace Microsoft.Azure.Documents.Telemetry
{
    internal class DefaultCosmosDistributedContextPropagator : CosmosDistributedContextPropagatorBase
    {
        private readonly Action<Activity, Action<string, string>> propagator;

        internal DefaultCosmosDistributedContextPropagator() : 
            this(static (activity, action) =>
            {
                action(HttpConstants.HttpHeaders.TraceParent, activity.Id);
                action(HttpConstants.HttpHeaders.TraceState, activity.TraceStateString);
            })
        {
        }

        internal DefaultCosmosDistributedContextPropagator(Action<Activity, Action<string, string>> propagator)
        {
            this.propagator = propagator;
        }

        internal override void Inject(Activity activity, INameValueCollection headers)
        {
            if (activity == null || headers == null)
            {
                return;
            }

            this.propagator(activity, (fieldName, fieldValue) => headers.Set(fieldName, fieldValue));
        }
    }
}
#endif