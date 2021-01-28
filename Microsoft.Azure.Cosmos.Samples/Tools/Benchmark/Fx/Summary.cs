//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace CosmosBenchmark
{
    using System;

    internal struct Summary
    {
        private const int MsPerSecond = 1000;

        public long successfulOpsCount;
        public long failedOpsCount;
        public double ruCharges;
        public double elapsedMs;

        public double Rups()
        {
            return Math.Round(
                    Math.Min(this.ruCharges / this.elapsedMs * Summary.MsPerSecond, this.ruCharges),
                    2);
        }

        public double Rps()
        {
            return Math.Round(
                    Math.Min(this.successfulOpsCount / this.elapsedMs * Summary.MsPerSecond, this.successfulOpsCount),
                    2);
        }

        public void Print(long globalTotal)
        {
            Utility.TeePrint("Stats, total: {0,5}   success: {1,5}   fail: {2,3}   RPs: {3,5}   RUps: {4,5}",
                globalTotal,
                this.successfulOpsCount,
                this.failedOpsCount,
                this.Rps(),
                this.Rups());
        }

        public static Summary operator +(Summary arg1, Summary arg2)
        {
            return new Summary()
            {
                successfulOpsCount = arg1.successfulOpsCount + arg2.successfulOpsCount,
                failedOpsCount = arg1.failedOpsCount + arg2.failedOpsCount,
                ruCharges = arg1.ruCharges + arg2.ruCharges,
                elapsedMs = arg1.elapsedMs + arg2.elapsedMs,
            };
        }

        public static Summary operator -(Summary arg1, Summary arg2)
        {
            return new Summary()
            {
                successfulOpsCount = arg1.successfulOpsCount - arg2.successfulOpsCount,
                failedOpsCount = arg1.failedOpsCount - arg2.failedOpsCount,
                ruCharges = arg1.ruCharges - arg2.ruCharges,
                elapsedMs = arg1.elapsedMs - arg2.elapsedMs,
            };
        }
    }
}
