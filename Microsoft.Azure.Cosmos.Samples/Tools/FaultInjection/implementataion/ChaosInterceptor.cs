//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    internal class ChaosInterceptorFactory : IChaosInterceptorFactory
    {
        private readonly List<FaultInjectionRule> rules;

        public ChaosInterceptor? ChaosInterceptor { get; private set; }

        public ChaosInterceptorFactory(List<FaultInjectionRule> rules)
        {
            this.rules = rules;
        }

        public IChaosInterceptor CreateInterceptor(DocumentClient documentClient)
        {
            this.ChaosInterceptor ??= new ChaosInterceptor(this.rules, documentClient);

            return this.ChaosInterceptor;
        }
    }

    internal class ChaosInterceptor : IChaosInterceptor
    {
        private readonly List<FaultInjectionRule> rules;
        private readonly FaultInjectionRuleStore? ruleStore;
        private readonly RntbdConnectionErrorInjector? connectionErrorInjector;
        private readonly FaultInjectionDynamicChannelStore channelStore;
        private readonly FaultInjectionApplicationContext applicationContext;
        private readonly TimeSpan requestTimeout;

        public ChaosInterceptor(List<FaultInjectionRule> rules, DocumentClient documentClient)
        {
            this.rules = rules;
            this.channelStore = new FaultInjectionDynamicChannelStore();
            this.applicationContext = new FaultInjectionApplicationContext();
            this.ruleStore = new FaultInjectionRuleStore(documentClient, this.applicationContext);
            this.connectionErrorInjector = new RntbdConnectionErrorInjector(this.ruleStore, this.channelStore);
            this.requestTimeout = documentClient.ConnectionPolicy.RequestTimeout;
            this.ConfigureFaultInjectionRules();
        }

        private async void ConfigureFaultInjectionRules()
        {
            foreach (FaultInjectionRule rule in this.rules)
            {
                if (this.ruleStore != null)
                {
                    IFaultInjectionRuleInternal? effectiveRule = await this.ruleStore.ConfigureFaultInjectionRuleAsync(rule);
                    if (effectiveRule != null) { this.connectionErrorInjector?.Accept(effectiveRule); }
                }               
            }
        }

        /// <summary>
        /// Used to inject faults on request call
        /// </summary>
        /// <param name="args"></param>
        /// <param name="faultyResponse"></param>
        /// <returns></returns>
        public bool OnRequestCall(ChannelCallArguments args, out StoreResponse? faultyResponse)
        {

            FaultInjectionServerErrorRule? serverResponseErrorRule = this.ruleStore?.FindRntbdServerResponseErrorRule(args);
            if (serverResponseErrorRule != null)
            {
                this.applicationContext.AddRuleApplication(serverResponseErrorRule.GetId(), args.CommonArguments.ActivityId);

                faultyResponse = serverResponseErrorRule.GetInjectedServerError(args);

                DefaultTrace.TraceInformation("FaultInjection: FaultInjection Rule {0} Inserted error for request {1}",
                                    serverResponseErrorRule.GetId(), args.CommonArguments.ActivityId);

                if (serverResponseErrorRule.GetInjectedServerErrorType() == FaultInjectionServerErrorType.Timeout)
                {
                    TransportException transportException = new TransportException(
                        TransportErrorCode.RequestTimeout,
                        new TimeoutException("Fault Injection Server Error: Timeout"),
                        args.CommonArguments.ActivityId,
                        args.PreparedCall.Uri,
                        "Fault Injection Server Error: Timeout",
                        args.CommonArguments.UserPayload,
                        args.CommonArguments.PayloadSent);
                    Thread.Sleep(this.requestTimeout);
                    throw transportException;
                }

                return true;
            }

            faultyResponse = null;
            return false;
        }

        /// <summary>
        /// Used to inject faults on channel open
        /// </summary>
        /// <param name="activityId"></param>
        /// <param name="connectionCorrilationId"></param>
        /// <param name="serverUri"></param>
        /// <param name="openingRequest"></param>
        /// <param name="channel"></param>
        public void OnChannelOpen(Guid activityId, Guid connectionCorrilationId, Uri serverUri, DocumentServiceRequest openingRequest, Channel channel)
        {
            FaultInjectionServerErrorRule? serverConnectionDelayRule = this.ruleStore?.FindRntbdServerConnectionDelayRule(
                serverUri,
                openingRequest, 
                activityId);
            this.channelStore.AddChannel(connectionCorrilationId, channel);

            if (serverConnectionDelayRule != null)
            {
                serverConnectionDelayRule.GetDelay();
             
                this.applicationContext.AddRuleApplication(serverConnectionDelayRule.GetId(), activityId);

                DefaultTrace.TraceInformation("FaultInjection: FaultInjection Rule {0} Inserted {1} duration connection delay for request {2}",
                                    serverConnectionDelayRule.GetId(), serverConnectionDelayRule.GetDelay(), activityId);

                TimeSpan connectionDelay = serverConnectionDelayRule.GetDelay();
                Thread.Sleep(connectionDelay);
            }
        }

        /// <summary>
        /// Used to update internal active channel store on channel close
        /// </summary>
        /// <param name="connectionCorrelationId"></param>
        public void OnChannelDispose(Guid connectionCorrelationId)
        {
            this.channelStore.RemoveChannel(connectionCorrelationId);
        }

        /// <summary>
        /// Used to inject faults before connection writes
        /// </summary>
        /// <param name="args"></param>
        public void OnBeforeConnectionWrite(ChannelCallArguments args)
        {
            FaultInjectionServerErrorRule? serverResponseDelayRule = this.ruleStore?.FindRntbdServerResponseDelayRule(args);
                       
            if (serverResponseDelayRule != null)
            {
                this.applicationContext.AddRuleApplication(serverResponseDelayRule.GetId(), args.CommonArguments.ActivityId);
                TimeSpan delay = serverResponseDelayRule.GetDelay();

                DefaultTrace.TraceInformation("FaultInjection: FaultInjection Rule {0} Inserted {1} duration response delay for request {2}",
                                    serverResponseDelayRule.GetId(), delay, args.CommonArguments.ActivityId);

                Thread.Sleep(delay);
            }
        }

        /// <summary>
        /// Used to inject faults after connection writes
        /// </summary>
        /// <param name="args"></param>
        public void OnAfterConnectionWrite(ChannelCallArguments args)
        {
            FaultInjectionServerErrorRule? serverResponseDelayRule = this.ruleStore?.FindRntbdServerResponseDelayRule(args);

            if (serverResponseDelayRule != null)
            {
                this.applicationContext.AddRuleApplication(serverResponseDelayRule.GetId(), args.CommonArguments.ActivityId);
                TimeSpan delay = serverResponseDelayRule.GetDelay();

                DefaultTrace.TraceInformation("FaultInjection: FaultInjection Rule {0} Inserted {1} duration response delay for request {2}",
                                    serverResponseDelayRule.GetId(), delay, args.CommonArguments.ActivityId);

                Thread.Sleep(delay);
            }
        }

        /// <summary>
        /// Gets the fault injection rule id for the given activity id
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>the fault injection rule id</returns>
        public string GetFaultInjectionRuleId(Guid activityId)
        {
            return this.applicationContext.GetApplicationByActivityId(activityId)?.Item2.ToString() ?? string.Empty;
        }

        public FaultInjectionApplicationContext GetApplicationContext()
        {
            return this.applicationContext;
        }

        internal FaultInjectionRuleStore? GetRuleStore()
        {
            return this.ruleStore;
        }

        internal TimeSpan GetRequestTimeout()
        {
            return this.requestTimeout;
        }

        internal FaultInjectionDynamicChannelStore GetChannelStore()
        {
            return this.channelStore;
        }
    }
}