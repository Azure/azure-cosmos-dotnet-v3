//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    // DO NOT USE - use ClientDiagnosticListener instead
    public class TestDiagnosticListener : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly Func<DiagnosticListener, bool> selector;

        private List<IDisposable> subscriptions = new();

        public List<DiagnosticListener> Sources { get; } = new();
        public Action<(string Key, object Value, DiagnosticListener Listener)> EventCallback { get; set; }
        public Queue<(string Key, object Value, DiagnosticListener Listener)> Events { get; } = new();

        public Queue<(string Name, object Arg1, object Arg2)> IsEnabledCalls { get; } = new();

        public TestDiagnosticListener(string name) : this(source => source.Name == name)
        {
        }

        public TestDiagnosticListener(Func<DiagnosticListener, bool> selector)
        {
            this.selector = selector;
            DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener value)
        {
            List<IDisposable> subscriptions = this.subscriptions;
            if (this.selector(value) && subscriptions != null)
            {
                lock (subscriptions)
                {
                    this.Sources.Add(value);
                    subscriptions.Add(value.Subscribe(new InternalListener(evt =>
                    {
                        lock (this.Events)
                        {
                            this.Events.Enqueue(evt);
                        }

                        this.EventCallback?.Invoke(evt);
                    }, value), this.IsEnabled));
                }
            }
        }

        private bool IsEnabled(string arg1, object arg2, object arg3)
        {
            lock (this.IsEnabledCalls)
            {
                this.IsEnabledCalls.Enqueue((arg1, arg2, arg3));
            }
            return true;
        }

        public void Dispose()
        {
            if (this.subscriptions != null)
            {
                List<IDisposable> subscriptions = null;

                lock (this.subscriptions)
                {
                    if (this.subscriptions != null)
                    {
                        subscriptions = this.subscriptions;
                        this.subscriptions = null;
                    }
                }

                if (subscriptions != null)
                {
                    foreach (IDisposable subscription in subscriptions)
                    {
                        subscription.Dispose();
                    }
                }
            }
        }

        private class InternalListener : IObserver<KeyValuePair<string, object>>
        {
            private readonly Action<(string, object, DiagnosticListener)> queue;

            private readonly DiagnosticListener listener;

            public InternalListener(
                Action<(string Key, object Value, DiagnosticListener Listener)> eventCallback,
                DiagnosticListener listener)
            {
                this.queue = eventCallback;
                this.listener = listener;
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(KeyValuePair<string, object> value)
            {
                this.queue((value.Key, value.Value, this.listener));
            }
        }
    }
}
