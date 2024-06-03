//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;

    /// <summary>
    /// Fault Injection Server Error Result.
    /// </summary>
    public sealed class FaultInjectionServerErrorResult : IFaultInjectionResult
    {
        private readonly FaultInjectionServerErrorType serverErrorType;
        private readonly int times;
        private readonly TimeSpan delay;
        private readonly bool suppressServiceRequests;
        private readonly double injectionRate;

        /// <summary>
        /// Creates a new FaultInjectionServerErrorResult.
        /// </summary>
        /// <param name="serverErrorType">Specifies the server error type.</param>
        /// <param name="times">Specifies the number of times a rule can be applied on a single operation.</param>
        /// <param name="delay">Specifies the injected delay for the server error.</param>
        /// <param name="injectionRate">Specifies the percentage of how many times the rule will be applied.</param>
        public FaultInjectionServerErrorResult(
            FaultInjectionServerErrorType serverErrorType, 
            int times, 
            TimeSpan delay, 
            bool suppressServiceRequests,
            double injectionRate = 1)
        {
            this.serverErrorType = serverErrorType;
            this.times = times;
            this.delay = delay;
            this.suppressServiceRequests = suppressServiceRequests;
            this.injectionRate = injectionRate;
        }

        /// <summary>
        /// Gets the fault injection server error type.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionServerErrorType"/>.</returns>
        public FaultInjectionServerErrorType GetServerErrorType()
        {
            return this.serverErrorType;
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
        /// Required for RESPONSE_DELAY and CONNECTION_DELAY error types. 
        /// </summary>
        /// <returns>A TimeSpan represeting the lenght of the delay.</returns>
        public TimeSpan GetDelay()
        {
            return this.delay;
        }

        /// <summary>
        /// Get a flag indicating whether service requests should be suppressed. If not specified (null) the default
        /// behavior is applied - only sending the request to the service when the delay is lower
        /// than the network request timeout.
        /// </summary>
        /// <returns>a flag indicating whether service requests should be suppressed.</returns>
        public bool GetSuppressServiceRequests()
        {
            return this.suppressServiceRequests;
        }

        /// <summary>
        /// Gets the percentage of how many times the rule will be applied.
        /// </summary>
        /// <returns></returns>
        public double GetInjectionRate()
        {
            return this.injectionRate;
        }

        /// <summary>
        /// To String method
        /// </summary>
        /// <returns>a string represeting the <see cref="FaultInjectionServerErrorResult"/>.</returns>
        public override string ToString()
        {
            return String.Format(
                "FaultInjectionServerErrorResult{{ serverErrorType: {0}, times: {1}, delay: {2}, applicationPercentage: {3}}",
                this.serverErrorType,
                this.times,
                this.delay,
                this.injectionRate);
        }
    }
}
