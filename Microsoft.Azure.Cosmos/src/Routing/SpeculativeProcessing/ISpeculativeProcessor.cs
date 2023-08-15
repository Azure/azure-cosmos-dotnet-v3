// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing.SpeculativeProcessing
{
    using System;

    /// <summary>
    /// Interface for speculative processor.
    /// </summary>
    public interface ISpeculativeProcessor
    {
        /// <summary>
        /// Gets the threshold of when to speculate.
        /// </summary>
        /// <returns>the threshold.</returns>
        TimeSpan GetThreshold();

        /// <summary>
        /// The delay to wait before speculating based on the step.
        /// </summary>
        /// <param name="step"></param>
        /// <returns>the delay</returns>
        TimeSpan GetThresholdStepDuration(int step);

        /// <summary>
        /// Checks to see if the speculative processor is enabled.
        /// </summary>
        /// <returns>a bool representing if it is enabled.</returns>
        bool IsEnabled();

        /// <summary>
        /// Enables the speculative processor.
        /// </summary>
        void Enable();

        /// <summary>
        /// Disables the speculative processor.
        /// </summary>
        void Disable();


    }
}
