//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Tuning knobs for cold-start metadata cache hedging. Used by
    /// <see cref="Cosmos.Routing.MetadataHedgingStrategy"/> when
    /// <see cref="CosmosClientOptions.EnableMetadataHedgingForColdStart"/> is
    /// effectively on.
    /// </summary>
    /// <remarks>
    /// See <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c> §5.1.
    /// </remarks>
    internal sealed class MetadataHedgingOptions
    {
        /// <summary>
        /// Default per-client concurrency budget for in-flight metadata hedges.
        /// Mirrors the design §5.11 default.
        /// </summary>
        public const int DefaultPerClientConcurrencyBudget = 8;

        /// <summary>
        /// Default step between hedge branches. Reserved for future use when
        /// <see cref="MaxHedgeBranchesPerAttempt"/> &gt; 1.
        /// </summary>
        public static readonly TimeSpan DefaultThresholdStep = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Time after which the hedge branch is dispatched if the primary has
        /// not produced an acceptable response. When <c>null</c>, defaults to
        /// <c>HttpTimeoutPolicy.FirstAttemptTimeout + 500&#160;ms</c> (today
        /// 1.5&#160;s).
        /// </summary>
        public TimeSpan? Threshold { get; set; }

        /// <summary>
        /// Reserved for future use when <see cref="MaxHedgeBranchesPerAttempt"/>
        /// &gt; 1. Default: 500&#160;ms.
        /// </summary>
        public TimeSpan? ThresholdStep { get; set; }

        /// <summary>
        /// Maximum simultaneous hedge branches per attempt. Default: 1.
        /// Values &gt; 1 are reserved for a future release.
        /// </summary>
        public int MaxHedgeBranchesPerAttempt { get; set; } = 1;

        /// <summary>
        /// Per-client cap on in-flight metadata hedges. Default: 8.
        /// </summary>
        public int PerClientConcurrencyBudget { get; set; } = DefaultPerClientConcurrencyBudget;
    }
}
