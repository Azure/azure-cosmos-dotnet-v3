// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class CosmosTraceDiagnostics : CosmosDiagnostics
    {
        private static readonly string userAgent = new UserAgentContainer().UserAgent;

        public CosmosTraceDiagnostics(ITrace trace, string userAgent = null)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            this.UserAgent = userAgent ?? CosmosTraceDiagnostics.userAgent;

            // Need to set to the root trace, since we don't know which layer of the stack the response message was returned from.
            ITrace rootTrace = trace;
            while (rootTrace.Parent != null)
            {
                rootTrace = rootTrace.Parent;
            }

            this.Value = rootTrace;
        }

        public ITrace Value { get; }

        public string UserAgent { get; }

        public override string ToString()
        {
            return this.ToJsonString();
        }

        public override TimeSpan GetClientElapsedTime()
        {
            return this.Value.Duration;
        }

        private string ToJsonString()
        {
            ReadOnlyMemory<byte> utf8String = this.WriteTraceToJsonWriter(JsonSerializationFormat.Text);
            return Encoding.UTF8.GetString(utf8String.Span);
        }

        private ReadOnlyMemory<byte> WriteTraceToJsonWriter(JsonSerializationFormat jsonSerializationFormat)
        {
            IJsonWriter jsonTextWriter = JsonWriter.Create(jsonSerializationFormat);

            jsonTextWriter.WriteObjectStart();

            jsonTextWriter.WriteFieldName("User Agent");
            jsonTextWriter.WriteStringValue(this.UserAgent);

            jsonTextWriter.WriteFieldName("Traces");
            TraceWriter.WriteTrace(jsonTextWriter, this.Value);

            jsonTextWriter.WriteObjectEnd();

            return jsonTextWriter.GetResult();
        }
    }
}
