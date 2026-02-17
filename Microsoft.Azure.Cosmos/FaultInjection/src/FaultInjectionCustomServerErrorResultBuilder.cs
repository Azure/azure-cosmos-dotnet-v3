//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;

    /// <summary>
    /// Used to create a <see cref="FaultInjectionCustomServerErrorResult"/> with custom status and substatus codes.
    /// </summary>
    /// <remarks>
    /// WARNING: This is an internal-only API for testing purposes. Using arbitrary status/substatus code combinations
    /// may produce unexpected behavior or results that do not accurately reflect real-world scenarios. 
    /// Use with caution and only in controlled test environments.
    /// 
    /// <example>
    /// Example usage:
    /// <code>
    /// // Create a custom server error with status code XXX and substatus code YYY
    /// FaultInjectionCondition condition = new FaultInjectionConditionBuilder()
    ///     .WithOperationType(FaultInjectionOperationType.ReadItem)
    ///     .Build();
    /// 
    /// FaultInjectionRule rule = new FaultInjectionRuleBuilder(
    ///     id: "customErrorRule",
    ///     condition: condition,
    ///     result: new FaultInjectionCustomServerErrorResultBuilder(XXX, YYY)
    ///         .WithTimes(1)
    ///         .Build())
    ///     .Build();
    /// </code>
    /// </example>
    /// </remarks>
    internal sealed class FaultInjectionCustomServerErrorResultBuilder
    {
        private readonly int statusCode;
        private readonly int subStatusCode;
        private int times = int.MaxValue;
        private TimeSpan delay;
        private bool suppressServiceRequest;
        private double injectionRate = 1;

        /// <summary>
        /// Creates a <see cref="FaultInjectionCustomServerErrorResultBuilder"/>.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to inject (e.g., 123).</param>
        /// <param name="subStatusCode">The substatus code to inject (e.g., 456789).</param>
        /// <remarks>
        /// This constructor allows creation of server errors with any status/substatus code combination,
        /// including codes that may not exist in the Cosmos DB service. This is intended for internal
        /// testing only and may result in unexpected behavior.
        /// </remarks>
        public FaultInjectionCustomServerErrorResultBuilder(int statusCode, int subStatusCode)
        {
            this.statusCode = statusCode;
            this.subStatusCode = subStatusCode;
        }

        /// <summary>
        /// Sets the number of times the same fault injection rule can be applied per operation, 
        /// by default there is no limit.
        /// </summary>
        /// <param name="times">The maximum number of times the same fault injection rule can be applied per operation.</param>
        /// <returns>The current <see cref="FaultInjectionCustomServerErrorResultBuilder"/>.</returns>
        public FaultInjectionCustomServerErrorResultBuilder WithTimes(int times)
        {
            this.times = times;
            return this;
        }

        /// <summary>
        /// Sets the injected delay time for the server error. 
        /// </summary>
        /// <param name="delay">The duration of the delay.</param>
        /// <returns>The current <see cref="FaultInjectionCustomServerErrorResultBuilder"/>.</returns>
        public FaultInjectionCustomServerErrorResultBuilder WithDelay(TimeSpan delay)
        {
            this.delay = delay;
            return this;
        }

        /// <summary>
        /// Sets whether the service request should be suppressed.
        /// </summary>
        /// <param name="suppressServiceRequest">True to suppress the service request, false otherwise.</param>
        /// <returns>The current <see cref="FaultInjectionCustomServerErrorResultBuilder"/>.</returns>
        public FaultInjectionCustomServerErrorResultBuilder WithSuppressServiceRequest(bool suppressServiceRequest)
        {
            this.suppressServiceRequest = suppressServiceRequest;
            return this;
        }

        /// <summary>
        /// Sets the injection rate (percentage of requests to inject this error).
        /// </summary>
        /// <param name="injectionRate">The injection rate, must be between 0 (exclusive) and 1 (inclusive).</param>
        /// <returns>The current <see cref="FaultInjectionCustomServerErrorResultBuilder"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when injectionRate is not within the valid range.</exception>
        public FaultInjectionCustomServerErrorResultBuilder WithInjectionRate(double injectionRate)
        {
            if (injectionRate <= 0 || injectionRate > 1)
            {
                throw new ArgumentOutOfRangeException($"Argument '{nameof(injectionRate)}' must be within the range (0, 1].");
            }

            this.injectionRate = injectionRate;
            return this;
        }

        /// <summary>
        /// Creates a new <see cref="FaultInjectionCustomServerErrorResult"/>.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionCustomServerErrorResult"/>.</returns>
        public FaultInjectionCustomServerErrorResult Build()
        {
            return new FaultInjectionCustomServerErrorResult(
                this.statusCode,
                this.subStatusCode,
                this.times,
                this.delay,
                this.suppressServiceRequest,
                this.injectionRate);
        }
    }
}
