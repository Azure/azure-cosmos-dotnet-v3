// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class CosmosTraceDiagnostics : CosmosDiagnostics
    {
        private static readonly string userAgent = new UserAgentContainer().UserAgent;

        public CosmosTraceDiagnostics(ITrace trace)
        {
            this.Value = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        public ITrace Value { get; }

        public override string ToString()
        {
            // Need to set to the root trace, since we don't know which layer of the stack the response message was returned from.
            ITrace rootTrace = this.Value;
            while (rootTrace.Parent != null)
            {
                rootTrace = rootTrace.Parent;
            }

            return $"User Agent: {userAgent} {Environment.NewLine}{TraceWriter.TraceToText(rootTrace)}";
        }
    }
}
