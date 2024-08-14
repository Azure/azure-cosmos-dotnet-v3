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
        /// Default constructor
        /// </summary>
        internal AvailabilityStrategy()
        {
        }

        /// <summary>
        ///  Used on a per request level to disable a client level AvailabilityStrategy
        /// </summary>
        /// <returns>something</returns>
        internal static AvailabilityStrategy DisabledStrategy()
        {
            return new DisabledAvailabilityStrategy();
        }

        /// <summary>
        /// After a request's duration passes a threshold, this strategy will send out
        /// hedged request to other regions. The first hedge request will be sent after the threshold. 
        /// After that, the strategy will send out a request every thresholdStep
        /// until the request is completed or regions are exausted
        /// </summary>
        /// <param name="threshold"> how long before SDK begins hedging</param>
        /// <param name="thresholdStep">Period of time between first hedge and next hedging attempts</param>
        /// <returns>something</returns>
        public static AvailabilityStrategy CrossRegionHedgingStrategy(TimeSpan threshold,
            TimeSpan? thresholdStep)
        {
            return new CrossRegionHedgingAvailabilityStrategy(threshold, thresholdStep);
        }
    }
}