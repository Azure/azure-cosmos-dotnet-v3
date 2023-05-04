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

        /// <summary>
        /// Creates a new FaultInjectionServerErrorResult.
        /// </summary>
        /// <param name="serverErrorType">Specifies the server error type.</param>
        /// <param name="times">Specifies the number of times a rule can be applied on a single operation.</param>
        /// <param name="delay">Specifies the injected delay for the server error.</param>
        public FaultInjectionServerErrorResult(FaultInjectionServerErrorType serverErrorType, int times, TimeSpan delay)
        {
            this.serverErrorType = serverErrorType;
            this.times = times;
            this.delay = delay;
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
        /// To String method
        /// </summary>
        /// <returns>A string represeting the <see cref="FaultInjectionServerErrorResult"/>.</returns>
        public override string ToString()
        {
            return String.Format(
                "FaultInjectionServerErrorResult{{ serverErrorType: {0}, times: {1}, delay: {2}}}",
                this.serverErrorType,
                this.times,
                this.delay);
        }
    }
}
