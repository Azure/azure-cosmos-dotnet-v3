//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.DiagnosticSource
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal class Subscriber : IObserver<DiagnosticListener>
    {
        private readonly IReadOnlyList<IObserver<KeyValuePair<string, object>>> listenersToSubscribe;

        public Subscriber(IReadOnlyList<IObserver<KeyValuePair<string, object>>> listenersToSubscribe)
        {
            this.listenersToSubscribe = listenersToSubscribe;
        }

        public void OnCompleted()
        {
            DefaultTrace.TraceInformation("Completed Successfully");
        }

        public void OnError(Exception error)
        {
            DefaultTrace.TraceError(error.ToString());
        }

        public void OnNext(DiagnosticListener source)
        {
            if (source.Name == CosmosDiagnosticSource.DiagnosticSourceName && this.listenersToSubscribe.Count > 0)
            {
                foreach (IObserver<KeyValuePair<string, object>> listenerToSubscribe in this.listenersToSubscribe)
                {
                    source.Subscribe(listenerToSubscribe); 
                }
            }
        }
    }
}
