//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#nullable enable

namespace Azure.Core.Pipeline
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    internal class DiagnosticScopeFactory
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private static Dictionary<string, DiagnosticListener>? listeners;
        private readonly string? resourceProviderNamespace;
        private readonly DiagnosticListener? source;

        public DiagnosticScopeFactory(string clientNamespace, string? resourceProviderNamespace, bool isActivityEnabled)
        {
            this.resourceProviderNamespace = resourceProviderNamespace;
            this.IsActivityEnabled = isActivityEnabled;
            if (this.IsActivityEnabled && DiagnosticScopeFactory.listeners != null)
            {
                var listeners = LazyInitializer.EnsureInitialized<Dictionary<string, DiagnosticListener>>(ref DiagnosticScopeFactory.listeners);

                lock (listeners!)
                {
                    if (!listeners.TryGetValue(clientNamespace, out this.source))
                    {
                        this.source = new DiagnosticListener(clientNamespace);
                        listeners[clientNamespace] = this.source;
                    }
                }
            }
        }

        public bool IsActivityEnabled { get; }

        public DiagnosticScope CreateScope(string name, DiagnosticScope.ActivityKind kind = DiagnosticScope.ActivityKind.Client)
        {
            if (this.source == null)
            {
                return default;
            }
            var scope = new DiagnosticScope(this.source.Name, name, this.source, kind);

            if (this.resourceProviderNamespace != null)
            {
                scope.AddAttribute("az.namespace", this.resourceProviderNamespace);
            }
            return scope;
        }
    }
}
