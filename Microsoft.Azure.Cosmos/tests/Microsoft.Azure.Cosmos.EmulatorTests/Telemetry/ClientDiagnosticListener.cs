//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;

    public class ClientDiagnosticListener : IObserver<KeyValuePair<string, object>>, IObserver<DiagnosticListener>, IDisposable
    {
        private readonly Func<string, bool> sourceNameFilter;
        private readonly AsyncLocal<bool> collectThisStack;

        private List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Action<ProducedDiagnosticScope> scopeStartCallback;

        public List<ProducedDiagnosticScope> Scopes { get; } = new List<ProducedDiagnosticScope>();

        public ClientDiagnosticListener(string name, bool asyncLocal = false, Action<ProducedDiagnosticScope> scopeStartCallback = default)
            : this(n => n == name, asyncLocal, scopeStartCallback)
        {
        }

        public ClientDiagnosticListener(Func<string, bool> filter, bool asyncLocal = false, Action<ProducedDiagnosticScope> scopeStartCallback = default)
        {
            if (asyncLocal)
            {
                this.collectThisStack = new AsyncLocal<bool> { Value = true };
            }
            this.sourceNameFilter = filter;
            this.scopeStartCallback = scopeStartCallback;
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

                var startSuffix = ".Start";
                var stopSuffix = ".Stop";
                var exceptionSuffix = ".Exception";

                if (value.Key.EndsWith(startSuffix))
                {
                    var name = value.Key[..^startSuffix.Length];
                    PropertyInfo propertyInfo = value.Value.GetType().GetTypeInfo().GetDeclaredProperty("Links");
                    var links = propertyInfo?.GetValue(value.Value) as IEnumerable<Activity> ?? Array.Empty<Activity>();

                    var scope = new ProducedDiagnosticScope()
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
                    var name = value.Key[..^stopSuffix.Length];
                    foreach (ProducedDiagnosticScope producedDiagnosticScope in this.Scopes)
                    {
                        if (producedDiagnosticScope.Activity.Id == Activity.Current.Id)
                        {
                            this.AssertTags(producedDiagnosticScope.Name, producedDiagnosticScope.Activity.Tags);

                            producedDiagnosticScope.IsCompleted = true;
                            return;
                        }
                    }
                    throw new InvalidOperationException($"Event '{name}' was not started");
                }
                else if (value.Key.EndsWith(exceptionSuffix))
                {
                    var name = value.Key[..^exceptionSuffix.Length];
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

        private void AssertTags(string name, IEnumerable<KeyValuePair<string, string>> tags)
        {
            foreach(KeyValuePair<string, string> tag in tags)
            {
                Console.WriteLine(name + " => " + tag.Key + " : " + tag.Value);
            }
            Console.WriteLine();
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
                var activity = producedDiagnosticScope.Activity;
                var operationName = activity.OperationName;
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
        }

        public ProducedDiagnosticScope AssertScopeStarted(string name, params KeyValuePair<string, string>[] expectedAttributes)
        {
            return this.AssertScopeStartedInternal(name, false, expectedAttributes);
        }

        private ProducedDiagnosticScope AssertScopeStartedInternal(string name, bool remove, params KeyValuePair<string, string>[] expectedAttributes)
        {
            lock (this.Scopes)
            {
                var foundScopeNames = this.Scopes.Select(s => s.Name);
                foreach (ProducedDiagnosticScope producedDiagnosticScope in this.Scopes)
                {
                    if (producedDiagnosticScope.Name == name)
                    {
                        foreach (KeyValuePair<string, string> expectedAttribute in expectedAttributes)
                        {
                            if (!producedDiagnosticScope.Activity.Tags.Contains(expectedAttribute))
                            {
                                throw new InvalidOperationException($"Attribute {expectedAttribute} not found, existing attributes: {string.Join(",", producedDiagnosticScope.Activity.Tags)}");
                            }
                        }

                        if (remove)
                        {
                            this.Scopes.Remove(producedDiagnosticScope);
                        }

                        return producedDiagnosticScope;
                    }
                }
                throw new InvalidOperationException($"Event '{name}' was not started. Found scope names:\n{string.Join("\n", foundScopeNames)}\n");
            }
        }

        public ProducedDiagnosticScope AssertScope(string name, params KeyValuePair<string, string>[] expectedAttributes)
        {
            return this.AssertScopeInternal(name, false, expectedAttributes);
        }

        public ProducedDiagnosticScope AssertAndRemoveScope(string name, params KeyValuePair<string, string>[] expectedAttributes)
        {
            return this.AssertScopeInternal(name, true, expectedAttributes);
        }

        private ProducedDiagnosticScope AssertScopeInternal(string name, bool remove,
            params KeyValuePair<string, string>[] expectedAttributes)
        {
            ProducedDiagnosticScope scope = this.AssertScopeStartedInternal(name, remove, expectedAttributes);
            if (!scope.IsCompleted)
            {
                throw new InvalidOperationException($"'{name}' is not completed");
            }

            return scope;
        }

        public ProducedDiagnosticScope AssertScopeException(string name, Action<Exception> action = null)
        {
            ProducedDiagnosticScope scope = this.AssertScopeStarted(name);

            if (scope.Exception == null)
            {
                throw new InvalidOperationException($"Scope '{name}' is not marked as failed");
            }

            action?.Invoke(scope.Exception);

            return scope;
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
