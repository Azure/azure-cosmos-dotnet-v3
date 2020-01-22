//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    internal sealed class CosmosDiagnosticsContextList : CosmosDiagnosticWriter
    {
        private List<CosmosDiagnosticWriter> contextList { get; }

        internal CosmosDiagnosticsContextList()
        {
            this.contextList = new List<CosmosDiagnosticWriter>();
        }

        internal void AddWriter(CosmosDiagnosticWriter diagnosticWriter)
        {
            this.contextList.Add(diagnosticWriter);
        }

        internal void Append(CosmosDiagnosticsContextList newContext)
        {
            this.contextList.Add(newContext);
        }

        internal override void WriteJsonObject(JsonWriter jsonWriter)
        {
            foreach (CosmosDiagnosticWriter writer in this.contextList)
            {
                writer.WriteJsonObject(jsonWriter);
            }
        }
    }
}
