//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    /// <summary>
    /// Reason a metadata hedge was not dispatched. Recorded in
    /// <see cref="MetadataHedgeDiagnostics"/> for supportability.
    /// </summary>
    internal enum MetadataHedgeSkipReason
    {
        None,
        OptInDisabled,
        PpafDisabled,
        GatewayKillSwitchOn,
        SingleRegion,
        NotColdStart,
        ResourceTypeNotSupported,
        NotFirstReadFeedPage,
        BudgetExhausted,
        AlreadyHedgedThisOperation,
        ExcludedRegionLeavesNoTarget,
        AuthModeNotEligibleForHedge,
    }

    /// <summary>
    /// Identifies the branch (primary or hedge) that produced a candidate
    /// metadata-hedge winner. Used to compose the per-branch overlay in
    /// <see cref="MetadataHedgingStrategy.IsAcceptableWinner"/>.
    /// </summary>
    internal enum HedgeBranch
    {
        Primary,
        Hedge,
    }
}
