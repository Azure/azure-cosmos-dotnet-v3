//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System.Threading;

    /// <summary>
    /// Diagnostic record attached to the request's trace. Fields populated by
    /// the orchestration thread for the eligibility / winner outcome;
    /// <see cref="LoserOutcome"/> / <see cref="HedgeOutcome"/> may be updated
    /// later from the background-cleanup continuation and are read/written via
    /// <see cref="Volatile"/>.
    /// </summary>
    internal sealed class MetadataHedgeDiagnostics
    {
        private string hedgeOutcome;
        private string loserOutcome;

        public bool Eligible { get; set; }

        public MetadataHedgeSkipReason SkipReason { get; set; }

        public string ResourceType { get; set; }

        public string PrimaryRegion { get; set; }

        public string HedgeRegion { get; set; }

        public double ThresholdMs { get; set; }

        public double? HedgeFiredElapsedMs { get; set; }

        public string WinningRegion { get; set; }

        public int TotalAttempts { get; set; }

        public string HedgeOutcome
        {
            get => Volatile.Read(ref this.hedgeOutcome);
            set => Volatile.Write(ref this.hedgeOutcome, value);
        }

        public string LoserOutcome
        {
            get => Volatile.Read(ref this.loserOutcome);
            set => Volatile.Write(ref this.loserOutcome, value);
        }
    }
}
