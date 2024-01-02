//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Cosmos availability strategy options. 
    /// Availability allow the SDK to send out additional cross region requests to help 
    /// reduce latency and increase availability. Currently there is one type of availability strategy, parallel request hedging. 
    /// </summary>
    public class AvailabilityStrategyOptions
    {
        /// <summary>
        /// Constustor for availability strategy options
        /// </summary>
        /// <param name="availabilityStrategy"></param>
        /// <param name="enabled"></param>
        public AvailabilityStrategyOptions(AvailabilityStrategy availabilityStrategy, bool enabled = true)
        {
            this.AvailabilityStrategy = availabilityStrategy;
            this.Enabled = enabled;
        }

        /// <summary>
        /// Type of Availability Strategy
        /// </summary>
        public AvailabilityStrategy AvailabilityStrategy { get; }

        /// <summary>
        /// Whether or not the availability strategy is enabled
        /// </summary>
        public bool Enabled { get; private set; }
        
    }
}
