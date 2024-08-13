//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Types of availability strategies supported
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    abstract class AvailabilityStrategy
    {
        /// <summary>
        ///  stuff
        /// </summary>
        /// <returns>something</returns>
        public static AvailabilityStrategy DisabledAvailabilityStrategy()
        {
            return new DisabledAvailabilityStrategy();
        }

        /// <summary>
        /// stuff
        /// </summary>
        /// <param name="threshold"></param>
        /// <param name="thresholdStep"></param>
        /// <returns>something</returns>
        public static AvailabilityStrategy CrossRegionHedgingAvailabilityStrategy(TimeSpan threshold,
            TimeSpan? thresholdStep)
        {
            return new CrossRegionHedgingAvailabilityStrategy(threshold, thresholdStep);
        }

        public abstract string StrategyName { get; }
    }
}