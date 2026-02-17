//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;

    /// <summary>
    /// Fault Injection Custom Server Error Result with custom status and substatus codes.
    /// </summary>
    /// <remarks>
    /// WARNING: This is an internal-only API for testing purposes. Using arbitrary status/substatus code combinations
    /// may produce unexpected behavior or results that do not accurately reflect real-world scenarios. 
    /// Use with caution and only in controlled test environments.
    /// </remarks>
    internal sealed class FaultInjectionCustomServerErrorResult : IFaultInjectionResult
    {
        private readonly int statusCode;
        private readonly int subStatusCode;
        private readonly int times;
        private readonly TimeSpan delay;
        private readonly bool suppressServiceRequests;
        private readonly double injectionRate;

        /// <summary>
        /// Creates a new FaultInjectionCustomServerErrorResult.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to inject.</param>
        /// <param name="subStatusCode">The substatus code to inject.</param>
        /// <param name="times">Specifies the number of times a rule can be applied on a single operation.</param>
        /// <param name="delay">Specifies the injected delay for the server error.</param>
        /// <param name="suppressServiceRequests">Specifies whether to suppress the service request.</param>
        /// <param name="injectionRate">Specifies the percentage of how many times the rule will be applied.</param>
        internal FaultInjectionCustomServerErrorResult(
            int statusCode,
            int subStatusCode,
            int times,
            TimeSpan delay,
            bool suppressServiceRequests,
            double injectionRate = 1)
        {
            this.statusCode = statusCode;
            this.subStatusCode = subStatusCode;
            this.times = times;
            this.delay = delay;
            this.suppressServiceRequests = suppressServiceRequests;
            this.injectionRate = injectionRate;
        }

        /// <summary>
        /// Gets the custom HTTP status code.
        /// </summary>
        /// <returns>An int representing the HTTP status code.</returns>
        public int GetStatusCode()
        {
            return this.statusCode;
        }

        /// <summary>
        /// Gets the custom substatus code.
        /// </summary>
        /// <returns>An int representing the substatus code.</returns>
        public int GetSubStatusCode()
        {
            return this.subStatusCode;
        }

        /// <summary>
        /// Gets the number of times a rule can be applied on a single operation.
        /// </summary>
        /// <returns>An int representing the number of times a rule can be applied.</returns>
        public int GetTimes()
        {
            return this.times;
        }

        /// <summary>
        /// Gets the injected delay for the server error.
        /// </summary>
        /// <returns>A TimeSpan representing the length of the delay.</returns>
        public TimeSpan GetDelay()
        {
            return this.delay;
        }

        /// <summary>
        /// Get a flag indicating whether service requests should be suppressed. 
        /// </summary>
        /// <returns>a flag indicating whether service requests should be suppressed.</returns>
        public bool GetSuppressServiceRequests()
        {
            return this.suppressServiceRequests;
        }

        /// <summary>
        /// Gets the percentage of how many times the rule will be applied.
        /// </summary>
        /// <returns>A double representing the injection rate.</returns>
        public double GetInjectionRate()
        {
            return this.injectionRate;
        }

        /// <summary>
        /// To String method
        /// </summary>
        /// <returns>a string representing the <see cref="FaultInjectionCustomServerErrorResult"/>.</returns>
        public override string ToString()
        {
            return String.Format(
                "FaultInjectionCustomServerErrorResult{{ statusCode: {0}, subStatusCode: {1}, times: {2}, delay: {3}, injectionRate: {4}}}",
                this.statusCode,
                this.subStatusCode,
                this.times,
                this.delay,
                this.injectionRate);
        }
    }
}
