//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using Microsoft.Azure.Documents.FaultInjection;

    /// <summary>
    /// Used to build a <see cref="FaultInjectionConnectionErrorResult"/>.
    /// </summary>
    public sealed class FaultInjectionConnectionErrorResultBuilder
    {
        private readonly FaultInjectionConnectionErrorType connectionErrorType;
        private TimeSpan interval;
        private double threshold = 1.0;

        /// <summary>
        /// Creates a new instance of the <see cref="FaultInjectionConnectionErrorResult"/>.
        /// </summary>
        /// <param name="connectionErrorType"></param>
        public FaultInjectionConnectionErrorResultBuilder(FaultInjectionConnectionErrorType connectionErrorType)
        {
            this.connectionErrorType = connectionErrorType;
        }

        /// <summary>
        /// Indicates how often the connection error will be injected.
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public FaultInjectionConnectionErrorResultBuilder WithInterval(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Argument 'interval' must be greater than zero.");
            }

            this.interval = interval;
            return this;
        }
        /// <summary>
        /// Percentage of establised conection that will be impacted by the fault injection.
        /// Values must be between within the range (0, 1].
        /// The default value is 1.
        /// </summary>
        /// <param name="threshold"></param>
        /// <returns>the <see cref="FaultInjectionConnectionErrorResultBuilder"/>.</returns>
        public FaultInjectionConnectionErrorResultBuilder WithThreshold(double threshold)
        {
            if (threshold <= 0 || threshold > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(threshold), "Argument 'threshold' must be within the range (0, 1].");
            }

            this.threshold = threshold;
            return this;
        }

        /// <summary>
        /// Builds the <see cref="FaultInjectionConnectionErrorResult"/>.
        /// </summary>
        /// <returns>a <see cref="FaultInjectionConnectionErrorResult"/>.</returns>
        public FaultInjectionConnectionErrorResult Build()
        {
            return new FaultInjectionConnectionErrorResult(
                this.connectionErrorType,
                this.interval,
                this.threshold);
        }
    }
}
