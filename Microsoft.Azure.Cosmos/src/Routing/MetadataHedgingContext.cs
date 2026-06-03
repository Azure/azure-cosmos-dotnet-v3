//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Per-logical-operation context shared between
    /// <see cref="MetadataHedgingStrategy"/> and
    /// <c>MetadataRequestThrottleRetryPolicy</c>. Carries the cold-start signal,
    /// the dedupe set, the winner, the &quot;hedged this operation&quot; latch,
    /// and the first-page flag for PK-range pagination. See
    /// <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c> §5.2 / §6.1.
    /// </summary>
    internal sealed class MetadataHedgingContext
    {
        private Uri winningEndpoint;
        private string winningRegion;
        private int hasHedgedThisOperation;

        public bool IsColdStart { get; set; }

        public ResourceType ResourceType { get; set; }

        public bool IsFirstReadFeedPage { get; set; } = true;

        public ConcurrentDictionary<string, byte> AttemptedEndpoints { get; }
            = new ConcurrentDictionary<string, byte>();

        public Uri WinningEndpoint => Volatile.Read(ref this.winningEndpoint);

        public string WinningRegion => Volatile.Read(ref this.winningRegion);

        public bool HasHedgedThisOperation => Volatile.Read(ref this.hasHedgedThisOperation) == 1;

        /// <summary>
        /// Single-publication of the winning endpoint and region. Late loser
        /// continuations that try to re-publish observe a non-null existing
        /// value and leave it intact.
        /// </summary>
        internal void RecordWinner(Uri endpoint, string region)
        {
            Interlocked.CompareExchange(ref this.winningEndpoint, endpoint, null);
            Interlocked.CompareExchange(ref this.winningRegion, region, null);
        }

        /// <summary>
        /// Returns <c>true</c> if this caller is the first to mark the operation
        /// as having dispatched a hedge. Subsequent callers (across
        /// <c>BackoffRetryUtility</c> retries) observe <c>false</c> and skip.
        /// </summary>
        internal bool TryMarkHedgedThisOperation()
            => Interlocked.Exchange(ref this.hasHedgedThisOperation, 1) == 0;
    }
}
