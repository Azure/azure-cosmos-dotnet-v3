//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Telemetry.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class OpenTelemetryListener : IObserver<KeyValuePair<string, object>>, IObserver<DiagnosticListener>, IDisposable
    {
        private readonly Func<string, bool> sourceNameFilter;
        private readonly AsyncLocal<bool> collectThisStack;

        private List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Action<ProducedDiagnosticScope> scopeStartCallback;

        private List<ProducedDiagnosticScope> Scopes { get; } = new List<ProducedDiagnosticScope>();

        private List<string> Attributes { set;  get; }

        public OpenTelemetryListener(string name, bool asyncLocal = false, Action<ProducedDiagnosticScope> scopeStartCallback = default)
            : this(n => n == name, asyncLocal, scopeStartCallback)
        {
        }

        public OpenTelemetryListener(Func<string, bool> filter, bool asyncLocal = false, Action<ProducedDiagnosticScope> scopeStartCallback = default)
        {
            if (asyncLocal)
            {
                this.collectThisStack = new AsyncLocal<bool> { Value = true };
            }
            this.sourceNameFilter = filter;
            this.scopeStartCallback = scopeStartCallback;

            this.Attributes = new List<string>();

            DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (this.collectThisStack?.Value == false) return;

            lock (this.Scopes)
            {
                // Check for disposal
                if (this.subscriptions == null) return;

                string startSuffix = ".Start";
                string stopSuffix = ".Stop";
                string exceptionSuffix = ".Exception";

                if (value.Key.EndsWith(startSuffix))
                {
                    string name = value.Key[..^startSuffix.Length];
                    PropertyInfo propertyInfo = value.Value.GetType().GetTypeInfo().GetDeclaredProperty("Links");
                    IEnumerable<Activity> links = propertyInfo?.GetValue(value.Value) as IEnumerable<Activity> ?? Array.Empty<Activity>();

                    ProducedDiagnosticScope scope = new ProducedDiagnosticScope()
                    {
                        Name = name,
                        Activity = Activity.Current,
                        Links = links.Select(a => new ProducedLink(a.ParentId, a.TraceStateString)).ToList(),
                        LinkedActivities = links.ToList()
                    };

                    this.Scopes.Add(scope);
                    this.scopeStartCallback?.Invoke(scope);
                }
                else if (value.Key.EndsWith(stopSuffix))
                {
                    string name = value.Key[..^stopSuffix.Length];
                    foreach (ProducedDiagnosticScope producedDiagnosticScope in this.Scopes)
                    {
                        if (producedDiagnosticScope.Activity.Id == Activity.Current.Id)
                        {
                            this.RecordAttributes(producedDiagnosticScope.Name, producedDiagnosticScope.Activity.Tags);

                            producedDiagnosticScope.IsCompleted = true;
                            return;
                        }
                    }
                    throw new InvalidOperationException($"Event '{name}' was not started");
                }
                else if (value.Key.EndsWith(exceptionSuffix))
                {
                    string name = value.Key[..^exceptionSuffix.Length];
                    foreach (ProducedDiagnosticScope producedDiagnosticScope in this.Scopes)
                    {
                        if (producedDiagnosticScope.Activity.Id == Activity.Current.Id)
                        {
                            if (producedDiagnosticScope.IsCompleted)
                            {
                                throw new InvalidOperationException("Scope should not be stopped when calling Failed");
                            }

                            producedDiagnosticScope.Exception = (Exception)value.Value;
                        }
                    }
                }
            }
        }

        private void RecordAttributes(string name, IEnumerable<KeyValuePair<string, string>> tags)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<ACTIVITY>")
                   .Append("<OPERATION>")
                   .Append(name)
                   .Append("</OPERATION>");
            foreach (KeyValuePair<string, string> tag in tags)
            {
                if(tag.Key != OpenTelemetryAttributeKeys.RequestDiagnostics)
                {
                    builder
                   .Append("<ATTRIBUTE-KEY>")
                   .Append(tag.Key)
                   .Append("</ATTRIBUTE-KEY><ATTRIBUTE-VALUE>")
                   .Append(tag.Value)
                   .Append("</ATTRIBUTE-VALUE>");
                }
            }
            builder.Append("</ACTIVITY>");

            this.Attributes.Add(builder.ToString());
        }

        public List<string> GetRecordedAttributes() 
        {
            return this.Attributes;
        }

        public void ResetAttributes()
        {
            this.Attributes = new List<string>();
        }

        public void OnNext(DiagnosticListener value)
        {
            if (this.sourceNameFilter(value.Name) && this.subscriptions != null)
            {
                lock (this.Scopes)
                {
                    if (this.subscriptions != null)
                    {
                        this.subscriptions.Add(value.Subscribe(this));
                    }
                }
            }
        }

        public void Dispose()
        {
            if (this.subscriptions == null)
            {
                return;
            }

            List<IDisposable> subscriptions;
            lock (this.Scopes)
            {
                subscriptions = this.subscriptions;
                this.subscriptions = null;
            }

            foreach (IDisposable subscription in subscriptions)
            {
                subscription.Dispose();
            }

            foreach (ProducedDiagnosticScope producedDiagnosticScope in this.Scopes)
            {
                Activity activity = producedDiagnosticScope.Activity;
                string operationName = activity.OperationName;
                // traverse the activities and check for duplicates among ancestors
                while (activity != null)
                {
                    if (operationName == activity.Parent?.OperationName)
                    {
                        // Throw this exception lazily on Dispose, rather than when the scope is started, so that we don't trigger a bunch of other
                        // erroneous exceptions relating to scopes not being completed/started that hide the actual issue
                        throw new InvalidOperationException($"A scope has already started for event '{producedDiagnosticScope.Name}'");
                    }

                    activity = activity.Parent;
                }

                if (!producedDiagnosticScope.IsCompleted)
                {
                    throw new InvalidOperationException($"'{producedDiagnosticScope.Name}' scope is not completed");
                }
            }

            this.ResetAttributes();
        }

        public class ProducedDiagnosticScope
        {
            public string Name { get; set; }
            public Activity Activity { get; set; }
            public bool IsCompleted { get; set; }
            public bool IsFailed => this.Exception != null;
            public Exception Exception { get; set; }
            public List<ProducedLink> Links { get; set; } = new List<ProducedLink>();
            public List<Activity> LinkedActivities { get; set; } = new List<Activity>();

            public override string ToString()
            {
                return this.Name;
            }
        }

        public struct ProducedLink
        {
            public ProducedLink(string id)
            {
                this.Traceparent = id;
                this.Tracestate = null;
            }

            public ProducedLink(string traceparent, string tracestate)
            {
                this.Traceparent = traceparent;
                this.Tracestate = tracestate;
            }

            public string Traceparent { get; set; }
            public string Tracestate { get; set; }
        }
    }
}
