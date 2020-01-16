//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class CosmosDiagnosticsAggregate : CosmosDiagnosticsInternal, IEnumerable<CosmosDiagnosticsInternal>
    {
        private readonly IReadOnlyList<CosmosDiagnosticsInternal> diagnostics;

        public CosmosDiagnosticsAggregate(IReadOnlyList<CosmosDiagnosticsInternal> cosmosDiagnosticsInternals)
        {
            if (cosmosDiagnosticsInternals == null)
            {
                throw new ArgumentNullException(nameof(cosmosDiagnosticsInternals));
            }

            if (cosmosDiagnosticsInternals.Any(x => x == null))
            {
                throw new ArgumentException($"{nameof(cosmosDiagnosticsInternals)} must not have null arguments.");
            }

            this.diagnostics = cosmosDiagnosticsInternals;
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor cosmosDiagnosticsInternalVistor)
        {
            cosmosDiagnosticsInternalVistor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public IEnumerator<CosmosDiagnosticsInternal> GetEnumerator()
        {
            return this.diagnostics.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.diagnostics.GetEnumerator();
        }
    }
}
