//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using App.Metrics.ReservoirSampling;

    /// <summary>
    /// Returns the <see cref="IReservoir"/> based on the CTL configuration.
    /// </summary>
    public class ReservoirProvider
    {
        /// <summary>
        /// Create and returns a new instance of the <see cref="IReservoir"/> based on the Benchmark configuration.
        /// </summary>
        /// <param name="benchmarkConfig">An instance of <see cref="BenchmarkConfig"/> containing the Benchmark config params.</param>
        /// <returns>An implementation of <see cref="IReservoir"/>.</returns>
        public static IReservoir GetReservoir(BenchmarkConfig benchmarkConfig)
        {
            return benchmarkConfig.ReservoirType switch
            {
                ReservoirTypes.Uniform => new App.Metrics.ReservoirSampling.Uniform.DefaultAlgorithmRReservoir(
                    sampleSize: benchmarkConfig.ReservoirSampleSize),

                ReservoirTypes.SlidingWindow => new App.Metrics.ReservoirSampling.SlidingWindow.DefaultSlidingWindowReservoir(
                    sampleSize: benchmarkConfig.ReservoirSampleSize),

                ReservoirTypes.ExponentialDecay => new App.Metrics.ReservoirSampling.ExponentialDecay.DefaultForwardDecayingReservoir(
                    sampleSize: benchmarkConfig.ReservoirSampleSize,
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
