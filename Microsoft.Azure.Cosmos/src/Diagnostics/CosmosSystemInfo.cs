//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using Microsoft.Azure.Documents.Rntbd;

    internal sealed class CosmosSystemInfo : CosmosDiagnosticsInternal
    {
        public readonly CpuLoadHistory CpuLoadHistory;

        public CosmosSystemInfo(
            CpuLoadHistory cpuLoadHistory)
        {
            this.CpuLoadHistory = cpuLoadHistory ?? throw new ArgumentNullException(nameof(cpuLoadHistory));
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
