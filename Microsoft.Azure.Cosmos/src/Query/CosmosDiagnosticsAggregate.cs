//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    internal class CosmosDiagnosticsAggregate : CosmosDiagnostics
    {
        public IList<CosmosDiagnostics> Diagnostics = new List<CosmosDiagnostics>();

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
