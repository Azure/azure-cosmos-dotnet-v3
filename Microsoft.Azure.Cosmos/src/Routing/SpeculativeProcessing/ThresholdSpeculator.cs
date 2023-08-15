namespace Microsoft.Azure.Cosmos.Routing.SpeculativeProcessing
{
    using System;

    /// <summary>
    /// Threshold based speculator
    /// </summary>
    public class ThresholdSpeculator : ISpeculativeProcessor
    {
        private TimeSpan threshold;
        private bool enabled = true;

        /// <summary>
        /// The speculation type
        /// </summary>
        public SpeculationType SpeculationType = SpeculationType.THRESHOLD_BASED;

        /// <summary>
        /// Constructor for the ThresholdSpeculator
        /// </summary>
        /// <param name="threshold"></param>
        public ThresholdSpeculator(TimeSpan threshold)
        {
            this.threshold = threshold;
        }

        /// <summary>
        /// Gets the threshold of when to speculate.
        /// </summary>
        /// <returns>the threshold.</returns>
        public TimeSpan GetThreshold()
        {
            return this.threshold;
        }

        /// <summary>
        /// The delay to wait before speculating based on the step.
        /// </summary>
        /// <param name="step"></param>
        /// <returns>the delay</returns>
        public TimeSpan GetThresholdStepDuration(int step)
        {
            return TimeSpan.FromTicks(this.threshold.Ticks * step);
        }

        /// <summary>
        /// Checks whether the speculator is enabled.
        /// </summary>
        /// <returns>a bool representing whether it is enabled.</returns>
        public bool IsEnabled()
        {
            return this.enabled;
        }

        /// <summary>
        /// Enables speculation.
        /// </summary>
        public void Enable()
        {
            this.enabled = true;
        }

        /// <summary>
        /// Disables speculation.
        /// </summary>
        public void Disable()
        {
            this.enabled = false;
        }
    }
}
