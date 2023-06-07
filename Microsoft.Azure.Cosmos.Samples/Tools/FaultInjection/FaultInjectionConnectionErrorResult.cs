//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using Microsoft.Azure.Documents.FaultInjection;
    using System;

    /// <summary>
    /// Fault Injection Connection Error Result.
    /// </summary>
    public sealed class FaultInjectionConnectionErrorResult : IFaultInjectionResult
    {
        private readonly FaultInjectionConnectionErrorType connectionErrorType;
        private readonly TimeSpan interval;
        private readonly double threshold;

         /// <summary>
         /// Creates a new FaultInjectionConnectionErrorResult
         /// </summary>
         /// <param name="connectionErrorType">Specifies the connection error type.</param>
         /// <param name="interval">Timespan representing the ammount of time the SDK will wait before returning the error.</param>
         /// <param name="threshold">Percentage of the established connections that will be impaceted.</param>
        public FaultInjectionConnectionErrorResult(
            FaultInjectionConnectionErrorType connectionErrorType,
            TimeSpan interval,
            double threshold)
        {
            this.connectionErrorType = connectionErrorType;
            this.interval = interval;
            this.threshold = threshold;
        }

        /// <summary>
        /// Gets the Connection Error Type.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionConnectionErrorType"/>.</returns>
        public FaultInjectionConnectionErrorType GetConnectionErrorType()
        {
            return this.connectionErrorType;
        }

        /// <summary>
        /// The ammount of time the SDK will wait before returning the error.
        /// </summary>
        /// <returns>the Timespan</returns>
        public TimeSpan GetInterval()
        {
            return this.interval;
        }

        /// <summary>
        /// Returns the percentage of the established connections that will be impacted. By default, the threshold is 1.
        /// </summary>
        /// <returns>the threshold represented as a double.</returns>
        public double GetThreshold()
        {
            return this.threshold;
        }

        /// <summary>
        /// To String method
        /// </summary>
        /// <returns>A string represeting the <see cref="FaultInjectionConnectionErrorResult"/>.</returns>
        public override string ToString()
        {
            return String.Format(
                "FaultInjectionConnection{{ ConnectionErrorType: {0}, Interval: {1}, Threshold: {2}%}}",
                this.connectionErrorType,
                this.interval,
                this.threshold);
        }
    }
}
