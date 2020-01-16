//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    internal sealed class QueryAggregateDiagnostics : CosmosDiagnosticsInternal
    {
        public QueryAggregateDiagnostics(
            IReadOnlyCollection<QueryPageDiagnostics> pages)
        {
            this.Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        }

        public IReadOnlyCollection<QueryPageDiagnostics> Pages { get; }

        public override void Accept(CosmosDiagnosticsInternalVisitor cosmosDiagnosticsInternalVisitor)
        {
            cosmosDiagnosticsInternalVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
