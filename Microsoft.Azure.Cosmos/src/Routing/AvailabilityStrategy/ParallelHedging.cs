//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Parallel hedging availability strategy. Once threshold time is reached, 
    /// the SDK will send out an additional request to a remote region in parallel
    /// if the first parallel request or the original has not returned after the step time, 
    /// additional parallel requests will be sent out there is a response or all regions are exausted.
    /// </summary>
    public class ParallelHedging : AvailabilityStrategy
    {
        /// <summary>
        /// When the SDK decided to activate the availability strategy.
        /// </summary>
        private TimeSpan threshold;

        /// <summary>
        /// When the SDK will send out additional availability requests after the first one
        /// </summary>
        private TimeSpan step;

        /// <summary>
        /// Constustor for parallel hedging availability strategy
        /// </summary>
        /// <param name="threshold"></param>
        /// <param name="step"></param>
        public ParallelHedging(TimeSpan threshold, TimeSpan? step)
        {
            this.threshold = threshold;
            this.step = step ?? TimeSpan.MaxValue;
        }

        /// <summary>
        /// Threshold of when to start availability strategy
        /// </summary>
        public TimeSpan Threshold => this.threshold;

        /// <summary>
        /// Step time to wait before sending out additional parallel requests
        /// </summary>
        public TimeSpan Step => this.step;
    }
}
