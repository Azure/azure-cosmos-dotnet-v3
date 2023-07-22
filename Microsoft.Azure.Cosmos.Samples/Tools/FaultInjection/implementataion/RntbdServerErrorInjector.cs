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
            this.ruleStore = ruleStore ?? throw new ArgumentNullException(paramName: "ruleStore");
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
            FaultInjectionServerErrorRule? serverResponseDelayRule = this.ruleStore.FindRntbdServerResponseDelayRule(args);
            if (serverResponseDelayRule != null)
            {
                args.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        activityId: args.CommonArguments.ActivityId,//args.PreparedCall.RequestId,
                        ruleId: serverResponseDelayRule.GetId());

                transportRequestStats.RecordState(TransportRequestStats.RequestStage.Sent);
                transportRequestStats.FaultInjectionDelay = serverResponseDelayRule.GetDelay();
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
            FaultInjectionServerErrorRule? serverResponseErrorRule = this.ruleStore.FindRntbdServerResponseErrorRule(args);
            if (serverResponseErrorRule != null)
            {
                args.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        activityId: args.CommonArguments.ActivityId,//args.PreparedCall.RequestId,
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
        /// <param name="activityId"></param>
        /// <param name="callUri"></param>
        /// <param name="request"></param>
        /// <returns>a bool representing if the injection was sucessfull.</returns>
        public bool InjectRntbdServerConnectionDelay(
            Guid activityId,
            string callUri,
            DocumentServiceRequest request)
        {
            FaultInjectionServerErrorRule? serverConnectionDelayRule = this.ruleStore.FindRntbdServerConnectionDelayRule(
                activityId,
                callUri,
                request);

            if (serverConnectionDelayRule != null)
            {
                request.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        activityId: activityId,
                        ruleId: serverConnectionDelayRule.GetId());

                TimeSpan connectionDelay = serverConnectionDelayRule.GetDelay();
                Thread.Sleep(connectionDelay);
                return true;
            }
            return false;
        }
    }
}
