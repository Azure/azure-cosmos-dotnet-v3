//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Newtonsoft.Json;

    internal sealed class QueryAggregateDiagnostics : CosmosDiagnostics
    {
        public QueryAggregateDiagnostics(
            IReadOnlyCollection<QueryPageDiagnostics> pages)
        {
            if (pages == null)
            {
                throw new ArgumentNullException(nameof(pages));
            }

            this.Pages = pages;
        }

        [JsonProperty(PropertyName = "Pages")]
        public IReadOnlyCollection<QueryPageDiagnostics> Pages { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
