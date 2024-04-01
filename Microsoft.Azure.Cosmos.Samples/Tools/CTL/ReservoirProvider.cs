//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using App.Metrics.ReservoirSampling;

    /// <summary>
    /// Returns the <see cref="IReservoir"/> based on the CTL configuration.
    /// </summary>
    public class ReservoirProvider
    {
        /// <summary>
        /// Create and returns a new instance of the <see cref="IReservoir"/> based on the CTL configuration.
        /// </summary>
        /// <param name="ctlConfig">An instance of <see cref="CTLConfig"/> containing the CTL config params.</param>
        /// <returns>An implementation of <see cref="IReservoir"/>.</returns>
        public static IReservoir GetReservoir(CTLConfig ctlConfig)
        {
            return ctlConfig.ReservoirType switch
            {
                ReservoirTypes.Uniform => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir(
                    sampleSize: ctlConfig.ReservoirSampleSize),

                ReservoirTypes.SlidingWindow => new App.Metrics.ReservoirSampling.SlidingWindow.DefaultSlidingWindowReservoir(
                    sampleSize: ctlConfig.ReservoirSampleSize),

                ReservoirTypes.ExponentialDecay => new App.Metrics.ReservoirSampling.ExponentialDecay.DefaultForwardDecayingReservoir(
                    sampleSize: ctlConfig.ReservoirSampleSize,
                    alpha: 0.015),

                _ => throw new ArgumentException(
                    message: "Invalid ReservoirType Specified."),
            };
        }

        /// <summary>
        /// An enum containing different reservoir types.
        /// </summary>
        public enum ReservoirTypes
        {
            Uniform,
            SlidingWindow,
            ExponentialDecay
        }
    }
}
