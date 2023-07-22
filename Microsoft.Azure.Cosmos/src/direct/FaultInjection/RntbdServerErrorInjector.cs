//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    using Microsoft.Azure.Documents.Rntbd;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Rntbd server error injector
    /// </summary>
    internal class RntbdServerErrorInjector : IRntbdServerErrorInjector
    {
        private readonly List<IRntbdServerErrorInjector> faultInjectors = new List<IRntbdServerErrorInjector>();

        public void RegisterServerErrorInjector(IRntbdServerErrorInjector serverErrorInjector)
        {
            if (serverErrorInjector == null)
            {
                throw new ArgumentNullException("serverErrorInjector");
            }
            this.faultInjectors.Add(serverErrorInjector);
        }

        /// <summary>
        /// Injects a delay in the RNTBD server response
        /// </summary>
        /// <param name="request"></param>
        /// <param name="delay"></param>
        /// <returns>a bool representing if the injection was sucessfull.</returns>
        public bool InjectRntbdServerResponseDelay(
            ChannelCallArguments args,
            TransportRequestStats transportRequestStats)
        {
            foreach (IRntbdServerErrorInjector injector in this.faultInjectors)
            {
                if (injector.InjectRntbdServerResponseDelay(args, transportRequestStats))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Injects a server error in the RNTBD server response
        /// </summary>
        /// <param name="args"></param>
        /// <param name="transportRequestStats"></param>
        /// <returns>a bool representing if the injection was sucessfull.</returns>
        public bool InjectRntbdServerResponseError(
            ChannelCallArguments args,
            TransportRequestStats transportRequestStats)
        {
            foreach (IRntbdServerErrorInjector injector in this.faultInjectors)
            {
                if (injector.InjectRntbdServerResponseError(args, transportRequestStats))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Injects a delay in the RNTBD server connection
        /// </summary>
        /// <param name="activityId"></param>
        /// <param name="callUri"></param>
        /// <param name="request"></param>
        /// <returns>a bool representing if the injection was sucessfull.</returns>
        public bool InjectRntbdServerConnectionDelay(
            Guid activityId,
            string callUri,
            DocumentServiceRequest request)
        {
            foreach (IRntbdServerErrorInjector injector in this.faultInjectors)
            {
                if (injector.InjectRntbdServerConnectionDelay(
                    activityId,
                    callUri,
                    request))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
