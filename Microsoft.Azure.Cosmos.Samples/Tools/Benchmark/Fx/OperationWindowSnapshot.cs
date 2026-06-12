//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    /// <summary>
    /// Immutable result of rolling up a single metric collection window for one operation.
    /// Produced by <see cref="OperationWindowAggregator.SnapshotAndReset(double)"/>.
    /// </summary>
    public readonly struct OperationWindowSnapshot
    {
        public OperationWindowSnapshot(
            long count,
            long errors,
            double p50Ms,
            double p90Ms,
            double p99Ms,
            double meanMs,
            double ruPerSec,
            int errorStatusCode,
            string errorMessage)
        {
            this.Count = count;
            this.Errors = errors;
            this.P50Ms = p50Ms;
            this.P90Ms = p90Ms;
            this.P99Ms = p99Ms;
            this.MeanMs = meanMs;
            this.RuPerSec = ruPerSec;
            this.ErrorStatusCode = errorStatusCode;
            this.ErrorMessage = errorMessage;
        }

        /// <summary>Number of successful operations recorded in the window.</summary>
        public long Count { get; }

        /// <summary>Number of failed operations recorded in the window.</summary>
        public long Errors { get; }

        public double P50Ms { get; }

        public double P90Ms { get; }

        public double P99Ms { get; }

        public double MeanMs { get; }

        /// <summary>Total request units charged in the window divided by the window length in seconds.</summary>
        public double RuPerSec { get; }

        /// <summary>The most recent error status code observed in the window (0 if none).</summary>
        public int ErrorStatusCode { get; }

        /// <summary>The most recent error message observed in the window (null if none).</summary>
        public string ErrorMessage { get; }
    }
}
