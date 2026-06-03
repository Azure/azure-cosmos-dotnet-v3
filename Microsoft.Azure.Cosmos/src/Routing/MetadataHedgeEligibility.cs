//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    /// <summary>
    /// Output of <see cref="MetadataHedgingStrategy.EvaluateEligibility"/>.
    /// </summary>
    internal readonly struct MetadataHedgeEligibility
    {
        public bool IsEligible { get; }

        public MetadataHedgeSkipReason SkipReason { get; }

        public MetadataHedgeEligibility(bool isEligible, MetadataHedgeSkipReason skipReason)
        {
            this.IsEligible = isEligible;
            this.SkipReason = skipReason;
        }

        public static MetadataHedgeEligibility Eligible() => new MetadataHedgeEligibility(true, MetadataHedgeSkipReason.None);

        public static MetadataHedgeEligibility Skip(MetadataHedgeSkipReason reason) => new MetadataHedgeEligibility(false, reason);
    }
}
