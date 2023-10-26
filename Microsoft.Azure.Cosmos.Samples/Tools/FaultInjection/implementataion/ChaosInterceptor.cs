//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using Microsoft.Azure.Documents;

    internal class ChaosInterceptor : IChaosInterceptor
    {
        /// <summary>
        /// Used to inject faults on request call
        /// </summary>
        /// <param name="args"></param>
        /// <param name="faultyResponse"></param>
        /// <returns></returns>
        public bool OnRequestCall(ChannelCallArguments args, out StoreResponse faultyResponse)
        {
            transportRequestStats.RecordState(TransportRequestStats.RequestStage.Sent);

            FaultInjectionServerErrorRule? serverResponseErrorRule = this.ruleStore.FindRntbdServerResponseErrorRule(args);
            if (serverResponseErrorRule != null)
            {
                args.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        activityId: args.CommonArguments.ActivityId,
                        ruleId: serverResponseErrorRule.GetId());

                serverResponseErrorRule.SetInjectedServerError(
                    args,
                    transportRequestStats);

                DefaultTrace.TraceInformation("FaultInjection: FaultInjection Rule {0} Inserted {1} error for request {2}",
                                    serverResponseErrorRule.GetId(), transportRequestStats.FaultInjectionDelay, args.CommonArguments.ActivityId);

                if (transportRequestStats.FaultInjectionServerErrorType == FaultInjectionServerErrorType.Timeout)
                {
                    Thread.Sleep(args.RequestTimeoutInSeconds * 1000);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Used to inject faults on channel open
        /// </summary>
        /// <param name="activityId"></param>
        /// <param name="serverUri"></param>
        /// <param name="openingRequest"></param>
        /// <param name="channel"></param>
        public void OnChannelOpen(Guid activityId, Uri serverUri, DocumentServiceRequest openingRequest, Channel channel)
        {
            FaultInjectionServerErrorRule? serverConnectionDelayRule = this.ruleStore.FindRntbdServerConnectionDelayRule(
                activityId,
                callUri,
                request);

            serverConnectionDelayRule.GetDelay();

            if (serverConnectionDelayRule != null)
            {
                request.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        activityId: activityId,
                        ruleId: serverConnectionDelayRule.GetId());

                DefaultTrace.TraceInformation("FaultInjection: FaultInjection Rule {0} Inserted {1} connection delay for request {2}",
                                    serverConnectionDelayRule.GetId(), serverConnectionDelayRule.GetDelay(), activityId);

                TimeSpan connectionDelay = serverConnectionDelayRule.GetDelay();
                Thread.Sleep(connectionDelay);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Used to update internal active channel store on channel close
        /// </summary>
        /// <param name="connectionCorrelationId"></param>
        public void OnChannelDispose(Guid connectionCorrelationId)
        {

        }

        /// <summary>
        /// Used to inject faults before connection writes
        /// </summary>
        /// <param name="args"></param>
        public void OnBeforeConnectionWrite(ChannelCallArguments args)
        {
            FaultInjectionServerErrorRule? serverResponseDelayRule = this.ruleStore.FindRntbdServerResponseDelayRule(args);
            DefaultTrace.TraceInformation("FaultInjection: FaultInjection Rule {0} Inserted {1} response delay for request {2}",
                                    serverResponseDelayRule.GetId(), transportRequestStats.FaultInjectionDelay, args.CommonArguments.ActivityId);
            if (serverResponseDelayRule != null)
            {
                args.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        activityId: args.CommonArguments.ActivityId,
                        ruleId: serverResponseDelayRule.GetId());

                transportRequestStats.RecordState(TransportRequestStats.RequestStage.Sent);
                transportRequestStats.FaultInjectionDelay = serverResponseDelayRule.GetDelay();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Used to inject faults after connection writes
        /// </summary>
        /// <param name="args"></param>
        public void OnAfterConnectionWrite(ChannelCallArguments args)
        {
            FaultInjectionServerErrorRule? serverResponseDelayRule = this.ruleStore.FindRntbdServerResponseDelayRule(args);
            DefaultTrace.TraceInformation("FaultInjection: FaultInjection Rule {0} Inserted {1} response delay for request {2}",
                                    serverResponseDelayRule.GetId(), transportRequestStats.FaultInjectionDelay, args.CommonArguments.ActivityId);
            if (serverResponseDelayRule != null)
            {
                args.FaultInjectionRequestContext
                    .ApplyFaultInjectionRule(
                        activityId: args.CommonArguments.ActivityId,
                        ruleId: serverResponseDelayRule.GetId());

                transportRequestStats.RecordState(TransportRequestStats.RequestStage.Sent);
                transportRequestStats.FaultInjectionDelay = serverResponseDelayRule.GetDelay();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the fault injection rule id for the given activity id
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>the fault injection rule id</returns>
        public string GetFaultInjectionRuleId(Guid activityId)
        {

        }
    }
}