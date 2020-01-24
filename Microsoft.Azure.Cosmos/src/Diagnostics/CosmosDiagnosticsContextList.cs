//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    internal sealed class CosmosDiagnosticsContextList : CosmosDiagnosticsInternal, IEnumerable<CosmosDiagnosticsInternal>
    {
        private List<CosmosDiagnosticsInternal> contextList { get; }

        public CosmosDiagnosticsContextList(List<CosmosDiagnosticsInternal> contextList)
        {
            this.contextList = contextList ?? throw new ArgumentNullException(nameof(contextList));
        }

        public CosmosDiagnosticsContextList()
            : this(new List<CosmosDiagnosticsInternal>())
        {
        }

        public void AddDiagnostics(CosmosDiagnosticsInternal cosmosDiagnosticsInternal)
        {
            if (cosmosDiagnosticsInternal == null)
            {
                throw new ArgumentNullException(nameof(cosmosDiagnosticsInternal));
            }

            this.contextList.Add(cosmosDiagnosticsInternal);
        }

        public void Append(CosmosDiagnosticsContextList newContext)
        {
            if (newContext == null)
            {
                throw new ArgumentNullException(nameof(newContext));
            }

            this.contextList.AddRange(newContext);
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor cosmosDiagnosticsInternalVisitor)
        {
            cosmosDiagnosticsInternalVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public IEnumerator<CosmosDiagnosticsInternal> GetEnumerator()
        {
            return this.contextList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.contextList.GetEnumerator();
        }
    }
}
