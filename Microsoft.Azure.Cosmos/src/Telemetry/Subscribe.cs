//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;

    internal class Subscribe : IObserver<DiagnosticListener>
    {
        private readonly IObserver<KeyValuePair<string, object>> clientTelemetryObserver = new ClientTelemetryCollector();
        private DiagnosticListener listener;

        public void OnCompleted()
        {
            this.listener?.Dispose();

            //Console.WriteLine("Client Telemetry Subscribed...Complete");
        }

        public void OnError(Exception error)
        {
            //Console.WriteLine("Client Telemetry Subscribed...Error => " + error.Message);
        }

        public void OnNext(DiagnosticListener value)
        {
            lock (this.clientTelemetryObserver)
            {
                this.listener = value;
                //Console.WriteLine("Came here to subscribe..." + value.Name);
                if (value.Name.Equals(ClientTelemetryOptions.DiagnosticSourceName))
                {
                    //Console.WriteLine("Client Telemetry Subscribed...");
                    value.Subscribe(this.clientTelemetryObserver);
                }
            }
        }
    }
}
