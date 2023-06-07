//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    using Microsoft.Azure.Documents.Rntbd;
    using System;

    /// <summary>
    /// Interface for RNTBD server error injector
    /// </summary>
    internal interface IRntbdServerErrorInjector
    {
        /// <summary>
        /// Injects a delay in the RNTBD server response
        /// </summary>
        /// <param name="request"></param>
        /// <param name="delay"></param>
        /// <returns>a bool representing if the injection was sucessfull.</returns>
        bool InjectRntbdServerResponseDelay(
            DocumentServiceRequest request,
            Action<TimeSpan> delay);

        /// <summary>
        /// Injects a server error in the RNTBD server response
        /// </summary>
        /// <param name="args"></param>
        /// <param name="transportRequestStats"></param>
        /// <returns>a bool representing if the injection was sucessfull.</returns>
        bool InjectRntbdServerResponseError(
            ChannelCallArguments args,
            TransportRequestStats transportRequestStats);

        /// <summary>
        /// Injects a delay in the RNTBD server connection
        /// </summary>
        /// <param name="request"></param>
        /// <param name="delay"></param>
        /// <returns>a bool representing if the injection was sucessfull.</returns>
        bool InjectRntbdServerConnectionDelay(
            DocumentServiceRequest request,
            Action<TimeSpan> delay);

    }
}
