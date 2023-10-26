namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Cosmos availability strategy options
    /// </summary>
    public class AvailabilityStrategyOptions
    {
        private TimeSpan threshold;
        private TimeSpan step;

        /// <summary>
        /// Constustor for availability strategy options
        /// </summary>
        /// <param name="availabilityStrategyType"></param>
        /// <param name="threshold"></param>
        /// <param name="step"></param>
        /// <param name="enabled"></param>
        public AvailabilityStrategyOptions(AvailabilityStrategyType availabilityStrategyType, TimeSpan threshold, TimeSpan? step, bool enabled = true)
        {
            this.AvailabilityStrategyType = availabilityStrategyType;
            this.threshold = threshold;
            this.step = step ?? TimeSpan.MaxValue;
            this.Enabled = enabled;
        }

        /// <summary>
        /// Type of Availability Strategy
        /// </summary>
        public AvailabilityStrategyType AvailabilityStrategyType { get; }

        /// <summary>
        /// Threshold of when to start availability strategy
        /// </summary>
        public TimeSpan Threshold => this.threshold;

        /// <summary>
        /// Step time to wait before sending out additional parallel requests
        /// </summary>
        public TimeSpan Step => this.step;

        /// <summary>
        /// Whether or not the availability strategy is enabled
        /// </summary>
        public bool Enabled { get; private set; }

        /// <summary>
        /// Enables availability strategy
        /// </summary>
        public void Enable()
        {
            this.Enabled = true;
        }
        
        /// <summary>
        /// Disables Availability Strategy
        /// </summary>
        public void Disable()
        {
            this.Enabled = false;
        }

        /// <summary>
        /// Updates the threshold time
        /// </summary>
        /// <param name="threshold"></param>
        public void UpdateThreshold(TimeSpan threshold)
        {
            this.threshold = threshold;
        }

        /// <summary>
        /// Updates the step time
        /// </summary>
        /// <param name="step"></param>
        public void UpdateStep(TimeSpan step)
        {
            this.step = step;
        }
    }
}
