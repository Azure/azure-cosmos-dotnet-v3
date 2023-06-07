//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents.FaultInjection;

    /// <summary>
    /// Rntbd server error injector
    /// </summary>
    internal class RntbdServerErrorInjector : IRntbdServerErrorInjector
    {
        private readonly FaultInjectionRuleStore ruleStore;

        public RntbdServerErrorInjector(FaultInjectionRuleStore ruleStore)
        {
            if (ruleStore == null)
            {
                throw new ArgumentNullExcpetion(paramName: "ruleStore");
            }
            this.ruleStore = ruleStore;
        }

        /// <summary>
        /// Injects a delay in the RNTBD server response
        /// </summary>
        /// <param name="request"></param>
        /// <param name="delay"></param>
        /// <returns>a bool representing if the injection was sucessfull.</returns>
        public bool InjectRntbdServerResponseDelay(
            DocumentServiceRequest request,
            Action<TimeSpan> delay)
        {
            FaultInjectionServerErrorRule serverResponseDelayRule = this.ruleStore.FindRntbdServerResponseDelayRule(request);
            if (serverResponseDelay != null)
            {
                request.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        serverResponseDelayRule.GetId());

                delay.Invoke(serverResponseDelayRule.Delay); ///or something like that
                return true;
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
            FaultInjectionServerErrorRule serverResponseErrorRule = this.ruleStore.FindRntbdServerResponseErrorRule(args);
            if (serverResponseErrorRule != null)
            {
                args.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        requestId: args.PreparedCall.RequestId,
                        ruleId: serverResponseErrorRule.GetId());

                serverResponseErrorRule.SetInjectedServerError(
                    args,
                    transportRequestStats);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Injects a delay in the RNTBD server connection
        /// </summary>
        /// <param name="request"></param>
        /// <param name="delay"></param>
        /// <returns>a bool representing if the injection was sucessfull.</returns>
        public bool InjectRntbdServerConnectionDelay(
            DocumentServiceRequest request,
            Action<TimeSpan> delay)
        {
            FaultInjectionServerErrorRule serverConnectionDelayRule = this.ruleStore.FindRntbdServerConnectionDelayRule(request);

            if (serverConnectionDelayRule != null)
            {
                request.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        serverConnectionDelayRule.GetId());

                delay.Invoke(serverConnectionDelayRule.Delay); ///or something like that
                return true;
            }
            return false;
        }
    }
}
