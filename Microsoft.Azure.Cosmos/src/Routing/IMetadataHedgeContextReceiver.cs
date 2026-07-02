//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    /// <summary>
    /// Narrow seam for handing a per-logical-operation
    /// <see cref="MetadataHedgingStrategy.MetadataHedgingContext"/> to whichever retry policy in the
    /// chain consumes it for cross-region dedup. Implemented by
    /// <c>MetadataRequestThrottleRetryPolicy</c> (which reads
    /// <see cref="MetadataHedgingStrategy.MetadataHedgingContext.AttemptedEndpoints"/> to skip regions a
    /// hedge already burned) and forwarded by retry-policy wrappers such as
    /// <c>ClearingSessionContainerClientRetryPolicy</c>.
    ///
    /// Replaces a fragile concrete-type <c>as</c> cast at the call site: if the policy is wrapped or
    /// replaced, the attach still reaches the inner consumer (or is an explicit, documented no-op)
    /// rather than silently degrading because the cast returned <c>null</c>.
    /// See <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c> §5.7.3 / §5.7.4.
    /// </summary>
    internal interface IMetadataHedgeContextReceiver
    {
        /// <summary>
        /// Attaches the hedging context for the current logical operation. Safe no-op when the
        /// receiver does not participate in metadata hedge dedup.
        /// </summary>
        /// <param name="context">The per-operation hedging context, or <c>null</c>.</param>
        void AttachHedgeContext(MetadataHedgingStrategy.MetadataHedgingContext context);
    }
}
