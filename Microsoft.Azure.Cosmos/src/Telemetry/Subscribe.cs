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
        private readonly ClientTelemetry telemetry;
        public Subscribe(ClientTelemetry telemetry)
        {
            this.telemetry = telemetry;
        }

        public void OnCompleted()
        {
           // Console.WriteLine("Client Telemetry Subscribed...Complete");
        }

        public void OnError(Exception error)
        {
            //Console.WriteLine("Client Telemetry Subscribed...Error => " + error.Message);
        }

        public void OnNext(DiagnosticListener value)
        {
            //Console.WriteLine("Came here to subscribe..." + value.Name);
            if (value.Name.Equals("ClientTelemetry"))
            {
               // Console.WriteLine("Client Telemetry Subscribed...");
                value.Subscribe(this.telemetry);
            }
        }
    }
}
