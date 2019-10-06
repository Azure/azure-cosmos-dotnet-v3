//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    internal sealed class QueryAggregateDiagnostics : CosmosDiagnostics
    {
        public QueryAggregateDiagnostics(
            IReadOnlyCollection<QueryPageDiagnostics> pages)
        {
            this.Pages = pages;
        }

        private IReadOnlyCollection<QueryPageDiagnostics> Pages { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this.Pages);
        }
    }
}
