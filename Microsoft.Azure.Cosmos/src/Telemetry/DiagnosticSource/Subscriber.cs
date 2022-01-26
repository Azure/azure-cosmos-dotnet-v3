//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.DiagnosticSource
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    internal class Subscriber : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly IReadOnlyList<IObserver<KeyValuePair<string, object>>> listenersToSubscribe;
        private readonly List<IDisposable> diagnosticListeners;
        private readonly IDisposable allDiagnosticListenersSubscription;

        public Subscriber(IReadOnlyList<IObserver<KeyValuePair<string, object>>> listenersToSubscribe)
        {
            this.listenersToSubscribe = listenersToSubscribe ?? throw new ArgumentNullException(nameof(listenersToSubscribe)); 

            this.diagnosticListeners = new List<IDisposable>();
            this.allDiagnosticListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in this.diagnosticListeners)
            {
                subscription.Dispose();
            }
            this.allDiagnosticListenersSubscription?.Dispose();
        }

        public void OnCompleted()
        {
            this.Dispose();
        }

        public void OnError(Exception error)
        {
            throw error;
        }

        public void OnNext(DiagnosticListener source)
        {
            if (source.Name == CosmosDiagnosticSource.DiagnosticSourceName)
            {
                foreach (IObserver<KeyValuePair<string, object>> listenerToSubscribe in this.listenersToSubscribe)
                {
                    this.diagnosticListeners.Add(source.Subscribe(listenerToSubscribe));
                }
            }
        }
    }
}
