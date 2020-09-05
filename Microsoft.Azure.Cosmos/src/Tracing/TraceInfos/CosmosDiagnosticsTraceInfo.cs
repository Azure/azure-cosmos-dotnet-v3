// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceInfos
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.Diagnostics;

    internal sealed class CosmosDiagnosticsTraceInfo : ITraceInfo
    {
        private readonly CosmosDiagnosticsInternal cosmosDiagnostics;

        public CosmosDiagnosticsTraceInfo(CosmosDiagnosticsInternal cosmosDiagnostics)
        {
            this.cosmosDiagnostics = cosmosDiagnostics ?? throw new ArgumentNullException(nameof(cosmosDiagnostics));
        }

        public string Serialize()
        {
            StringWriter writer = new StringWriter();
            CosmosDiagnosticsSerializerVisitor serializer = new CosmosDiagnosticsSerializerVisitor(writer);
            this.cosmosDiagnostics.Accept(serializer);
            return writer.ToString();
        }
    }
}
