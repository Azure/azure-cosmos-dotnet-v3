//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;

    /// <summary>
    /// Used to create a <see cref="FaultInjectionServerErrorResult"/>.
    /// </summary>
    public sealed class FaultInjectionServerErrorResultBuilder
    {
        private readonly FaultInjectionServerErrorType serverErrorType;
        private int times = int.MaxValue;
        private TimeSpan delay;
        private bool suppressServiceRequest;
        private bool isDelaySet = false;

        /// <summary>
        /// Creates a <see cref="FaultInjectionServerErrorResult"/>.
        /// </summary>
        /// <param name="serverErrorType"></param>
        public FaultInjectionServerErrorResultBuilder(FaultInjectionServerErrorType serverErrorType)
        {
            this.serverErrorType = serverErrorType;
        }

        /// <summary>
        /// Sets the number of times the same fault injection rule can be applied per operation, 
        /// by default there is no limit.
        /// </summary>
        /// <param name="times">The maximum number of times the same fault injection rule can be applied per operation.</param>
        /// <returns>The current <see cref="FaultInjectionServerErrorResultBuilder"/>.</returns>
        public FaultInjectionServerErrorResultBuilder WithTimes(int times)
        {
            this.times = times;
            return this;
        }

        /// <summary>
        /// Sets the injected delay time for the server error. 
        /// 
        /// Only used RESPONSE_DELAY and CONNECTION_DELAY.
        /// 
        /// For RESPONSE_DELAY, it is the delay added before the response.
        /// For CONNECTION_DELAY, it is the delay added before the connection is established.
        /// 
        /// </summary>
        /// <param name="delay">The duration of the delay.</param>
        /// <returns>The current <see cref="FaultInjectionServerErrorResultBuilder"/>.</returns>
        public FaultInjectionServerErrorResultBuilder WithDelay(TimeSpan delay)
        {
            if (this.serverErrorType == FaultInjectionServerErrorType.ResponseDelay 
                || this.serverErrorType == FaultInjectionServerErrorType.ConnectionDelay)
            {
                this.delay = delay;
                this.isDelaySet = true;
            }
            return this;
        }

        public FaultInjectionServerErrorResultBuilder WithSuppressServiceRequest(bool suppressServiceRequest)
        {
            this.suppressServiceRequest = suppressServiceRequest;
            return this;
        }

        /// <summary>
        /// Creates a new <see cref="FaultInjectionServerErrorResult"/>.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionServerErrorResult"/>.</returns>
        public FaultInjectionServerErrorResult Build()
        {
            if ((this.serverErrorType == FaultInjectionServerErrorType.ResponseDelay
                || this.serverErrorType == FaultInjectionServerErrorType.ConnectionDelay)
                && !this.isDelaySet)
            {
                throw new ArgumentNullException(nameof(this.delay), "Argument 'delay' required for server error type: " + this.serverErrorType);
            }

            return new FaultInjectionServerErrorResult(
                this.serverErrorType,
                this.times,
                this.delay,
                this.suppressServiceRequest);
        }
    }
}
