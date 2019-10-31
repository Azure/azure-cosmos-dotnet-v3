// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Core.Pipeline
{
    using System.Diagnostics;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    internal sealed class ClientDiagnostics
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly DiagnosticListener source;

        public ClientDiagnostics(string clientNamespace, bool isActivityEnabled)
        {
            if (isActivityEnabled)
            {
                this.source = new DiagnosticListener(clientNamespace);
            }
        }

        public ClientDiagnostics(ClientOptions options)
            : this(options.GetType().Namespace, options.Diagnostics.IsDistributedTracingEnabled)
        {
        }

        public DiagnosticScope CreateScope(string name)
        {
            if (this.source == null)
            {
                return default;
            }
            return new DiagnosticScope(name, this.source);
        }
    }
}