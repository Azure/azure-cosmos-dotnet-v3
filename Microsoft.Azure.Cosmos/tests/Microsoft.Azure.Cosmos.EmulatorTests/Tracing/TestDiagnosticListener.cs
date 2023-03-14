namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tests;

    internal class TestDiagnosticListener : 
        IObserver<DiagnosticListener>,
        IDisposable
    {
        private Func<string, bool> sourceNameFilter;

        private readonly TestListener listener 
            = new TestListener("Azure-Cosmos-Operation-Request-Diagnostics");
        
        private List<IDisposable> subscriptions = new();

        internal TestDiagnosticListener(string name)
          : this(n => Regex.Match(n, name).Success)
        {
        }
        
        internal TestDiagnosticListener(Func<string, bool> sourceNameFilter)
        {
            this.sourceNameFilter = sourceNameFilter;
            DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// IObserver Override
        /// </summary>
        public void OnNext(DiagnosticListener value)
        {
            if (this.sourceNameFilter(value.Name) && this.subscriptions != null)
            {
                lock (this.subscriptions)
                {
                    Console.WriteLine($"CustomListener: Subscribing to {value.Name}");
                    this.subscriptions?.Add(value.Subscribe(this.listener));
                }
            }
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
            this.listener.Dispose();

            this.sourceNameFilter = null;
        }

        public List<string> GetRecordedAttributes()
        {
            return this.listener.GetRecordedAttributes();
        }

        public void ResetAttributes()
        {
            this.listener.ResetAttributes();
        }
    }
}
