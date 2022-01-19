//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    internal class Subscriber : IObserver<DiagnosticListener>
    {
        private readonly IDictionary<string, IObserver<KeyValuePair<string, object>>> listenerToSubscribe;

        public Subscriber(IDictionary<string, IObserver<KeyValuePair<string, object>>> listenerToSubscribe)
        {
            this.listenerToSubscribe = listenerToSubscribe;
        }

        public void OnCompleted()
        {
            Console.WriteLine("successfully subscribed");
        }

        public void OnError(Exception error)
        {
            Console.WriteLine(error.ToString());
        }

        public void OnNext(DiagnosticListener listener)
        {
            foreach (KeyValuePair<string, IObserver<KeyValuePair<string, object>>> entry in listenerToSubscribe)
            {
                if (listener.Name == entry.Key)
                    listener.Subscribe(entry.Value);
            }
        }
    }
}
