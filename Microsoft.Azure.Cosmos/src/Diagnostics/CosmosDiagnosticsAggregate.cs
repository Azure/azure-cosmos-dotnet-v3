//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using System.Text;
    using Newtonsoft.Json;

    internal class CosmosDiagnosticsAggregate : CosmosDiagnostics, ICosmosDiagnosticsJsonWriter
    {
        public IList<CosmosDiagnostics> Diagnostics = new List<CosmosDiagnostics>();

        public void AppendJson(StringBuilder stringBuilder)
        {
            foreach (CosmosDiagnostics diagnostics in this.Diagnostics)
            {
                stringBuilder.Append(diagnostics.ToString());
            }
        }

        public override string ToString()
        {
            if (this.Diagnostics.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder stringBuilder = new StringBuilder();
            this.AppendJson(stringBuilder);
            return stringBuilder.ToString();
        }
    }
}
