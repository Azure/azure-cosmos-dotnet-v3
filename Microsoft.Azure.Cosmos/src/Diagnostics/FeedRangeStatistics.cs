//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;

    internal sealed class FeedRangeStatistics : CosmosDiagnosticsInternal
    {
        public FeedRangeStatistics(FeedRange feedRange)
        {
            this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
        }

        public FeedRange FeedRange { get; }

        public void Accept(CosmosDiagnosticsInternalVisitor cosmosDiagnosticsInternalVisitor)
        {
            cosmosDiagnosticsInternalVisitor.Visit(this);
        }

        public TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
