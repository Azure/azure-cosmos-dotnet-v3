//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal struct Summary
    {
        public long succesfulOpsCount;
        public long failedOpsCount;
        public double ruCharges;
        public double elapsedMs;

        public double Rups()
        {
            return Math.Round(this.ruCharges / this.elapsedMs * 1000, 2);
        }

        public double Rps()
        {
            return Math.Round((this.succesfulOpsCount + this.failedOpsCount) / this.elapsedMs * 1000, 2);
        }

        public void Print(long globalTotal)
        {
            Console.WriteLine("Stats, total: {0,5}   success: {1,5}   fail: {2,3}   RPS: {3,5}   rups: {4,5}",
                globalTotal,
                this.succesfulOpsCount,
                this.failedOpsCount,
                this.Rps(),
                this.Rups());
        }

        public static Summary operator +(Summary arg1, Summary arg2)
        {
            return new Summary()
            {
                succesfulOpsCount = arg1.succesfulOpsCount + arg2.succesfulOpsCount,
                failedOpsCount = arg1.failedOpsCount + arg2.failedOpsCount,
                ruCharges = arg1.ruCharges + arg2.ruCharges,
                elapsedMs = arg1.elapsedMs + arg2.elapsedMs,
            };
        }

        public static Summary operator -(Summary arg1, Summary arg2)
        {
            return new Summary()
            {
                succesfulOpsCount = arg1.succesfulOpsCount - arg2.succesfulOpsCount,
                failedOpsCount = arg1.failedOpsCount - arg2.failedOpsCount,
                ruCharges = arg1.ruCharges - arg2.ruCharges,
                elapsedMs = arg1.elapsedMs - arg2.elapsedMs,
            };
        }
    }
}
