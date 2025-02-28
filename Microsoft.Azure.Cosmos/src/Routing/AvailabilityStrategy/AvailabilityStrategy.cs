//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Types of availability strategies supported
    /// </summary>
    public abstract class AvailabilityStrategy
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
        public static AvailabilityStrategy DisabledStrategy()
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
        /// <returns>the cross region hedging availability strategy</returns>
        public static AvailabilityStrategy CrossRegionHedgingStrategy(
            TimeSpan threshold,
            TimeSpan? thresholdStep)
        {
            return new CrossRegionHedgingAvailabilityStrategy(threshold, thresholdStep, false);
        }

        /// <summary>
        /// After a request's duration passes a threshold, this strategy will send out
        /// hedged request to other regions. The first hedge request will be sent after the threshold. 
        /// After that, the strategy will send out a request every thresholdStep
        /// until the request is completed or regions are exausted
        /// </summary>
        /// <param name="threshold"> how long before SDK begins hedging</param>
        /// <param name="thresholdStep">Period of time between first hedge and next hedging attempts</param>
        /// <param name="enableMultiWriteRegionHedge">Whether hedging for write requests on accounts with multi-region writes are enabled
        /// Note that this does come with the caveat that there will be more 409 / 412 errors thrown by the SDK.
        /// This is expected and applications that adopt this feature should be prepared to handle these exceptions.
        /// Application might not be able to be deterministic on Create vs Replace in the case of Upsert Operations</param>
        /// <returns>the cross region hedging availability</returns>
#if PREVIEW
        public
#else
        internal
#endif
        static AvailabilityStrategy CrossRegionHedgingStrategy(
            TimeSpan threshold,
            TimeSpan? thresholdStep,
            bool enableMultiWriteRegionHedge = false)
        {
            return new CrossRegionHedgingAvailabilityStrategy(threshold, thresholdStep, enableMultiWriteRegionHedge);
        }
    }
}