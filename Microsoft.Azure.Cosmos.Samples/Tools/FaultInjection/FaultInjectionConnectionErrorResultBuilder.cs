//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;

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
        /// <param name="interval"></param>
        public FaultInjectionConnectionErrorResultBuilder(
            FaultInjectionConnectionErrorType connectionErrorType,
            TimeSpan interval)
        {
            this.connectionErrorType = connectionErrorType;
            this.interval = interval;
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
            if (this.interval != null)
            {
                throw new ArgumentNullException(nameof(this.interval), "Argument 'interval' cannot be null.");
            }

            return new FaultInjectionConnectionErrorResult(
                this.connectionErrorType,
                this.interval,
                this.threshold);
        }

    }
}
