//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.DiagnosticSource
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;

    internal class Subscriber : IObserver<DiagnosticListener>
    {
        private readonly IList<ICosmosDiagnosticListener> listenersToSubscribe;

        public Subscriber(IList<ICosmosDiagnosticListener> listenersToSubscribe)
        {
            this.listenersToSubscribe = listenersToSubscribe;
        }

        public void OnCompleted()
        {
            DefaultTrace.TraceInformation("Successfully Subscribed");
        }

        public void OnError(Exception error)
        {
            DefaultTrace.TraceError(error.ToString());
        }

        public void OnNext(DiagnosticListener source)
        {
            if (source.Name == CosmosDiagnosticSource.DiagnosticSourceName && this.listenersToSubscribe.Count > 0)
            {
                foreach (ICosmosDiagnosticListener listenerToSubscribe in this.listenersToSubscribe)
                {
                    source
                        .Subscribe(
                        observer: listenerToSubscribe.Listener,
                        (name, diagnostics, optionalObject) =>
                        {
                            if (listenerToSubscribe.DefaultFilter != null &&
                                    !name.Contains(listenerToSubscribe.DefaultFilter.ToString()))
                            {
                                return false;
                            }

                            if (listenerToSubscribe.Filter != null)
                            {
                                return listenerToSubscribe.Filter.Invoke((CosmosTraceDiagnostics)diagnostics);
                            }

                            return true;
                        });
                    
                }
            }
        }
    }
}
