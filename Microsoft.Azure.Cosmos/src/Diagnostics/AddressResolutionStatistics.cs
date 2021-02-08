//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;

    internal sealed class AddressResolutionStatistics : CosmosDiagnosticsInternal
    {
        public AddressResolutionStatistics(
            DateTime startTime,
            DateTime endTime,
            string targetEndpoint)
        {
            this.StartTime = startTime;
            this.EndTime = endTime;
            this.TargetEndpoint = targetEndpoint ?? throw new ArgumentNullException(nameof(startTime));
        }

        public DateTime StartTime { get; }
        public DateTime? EndTime { get; set; }
        public string TargetEndpoint { get; }

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
