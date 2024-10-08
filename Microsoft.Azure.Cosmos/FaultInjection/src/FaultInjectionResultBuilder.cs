//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    /// <summary>
    /// Fault Injection Result Builder.
    /// Based on error type will return either a <see cref="FaultInjectionServerErrorResultBuilder"/> or a <see cref="FaultInjectionConnectionErrorResultBuilder"/>.
    /// </summary>
    public sealed class FaultInjectionResultBuilder
    {

        /// <summary>
        /// Gets the server error result builder.
        /// </summary>
        /// <param name="serverErrorType">the server error type.</param>
        /// <returns>the fault injection server error builder.</returns>
        public static FaultInjectionServerErrorResultBuilder GetResultBuilder(FaultInjectionServerErrorType serverErrorType)
        {
            return new FaultInjectionServerErrorResultBuilder(serverErrorType);
        }

        /// <summary>
        /// Gets the connection error result builder.
        /// </summary>
        /// <param name="connectionErrorType">the connection error type.</param>
        /// <returns>the fault injection connection error builder.</returns>
        public static FaultInjectionConnectionErrorResultBuilder GetResultBuilder(FaultInjectionConnectionErrorType connectionErrorType)
        {
            return new FaultInjectionConnectionErrorResultBuilder(connectionErrorType);
        }
    }
}
