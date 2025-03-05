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
            this.ChaosInterceptor = new ChaosInterceptor(this.rules, documentClient);

            return this.ChaosInterceptor;
        }

        public async Task ConfigureChaosInterceptorAsync()
        {
            if (this.ChaosInterceptor != null)
            {
                await this.ChaosInterceptor.ConfigureFaultInjectionRules();
            }
        }
    }

    internal class ChaosInterceptor : IChaosInterceptor
    {
        private const string FaultInjectionId = "FaultInjectionId";

        private FaultInjectionRuleStore? ruleStore;
        private RntbdConnectionErrorInjector? connectionErrorInjector;
        private TimeSpan requestTimeout;

        private readonly DocumentClient documentClient;
        private readonly List<FaultInjectionRule> rules;
        private readonly FaultInjectionDynamicChannelStore channelStore;
        private readonly FaultInjectionApplicationContext applicationContext;

        public ChaosInterceptor(List<FaultInjectionRule> rules, DocumentClient documentClient)
        {
            this.documentClient = documentClient;
            this.rules = rules;
            this.channelStore = new FaultInjectionDynamicChannelStore();
            this.applicationContext = new FaultInjectionApplicationContext();
        }

        public async Task ConfigureFaultInjectionRules()
        {
            this.ruleStore = await FaultInjectionRuleStore.CreateAsync(this.documentClient, this.applicationContext);
            this.connectionErrorInjector = new RntbdConnectionErrorInjector(this.ruleStore, this.channelStore);
            this.requestTimeout = this.documentClient.ConnectionPolicy.RequestTimeout;

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
        /// <returns></returns>
        public async Task<(bool, StoreResponse?)> OnRequestCallAsync(ChannelCallArguments args)
        {
            StoreResponse faultyResponse;
            FaultInjectionServerErrorRule? serverResponseErrorRule = this.ruleStore?.FindRntbdServerResponseErrorRule(args);
            if (serverResponseErrorRule != null)
            {
                this.applicationContext.AddRuleExecution(serverResponseErrorRule.GetId(), args.CommonArguments.ActivityId);

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
                    await Task.Delay(this.requestTimeout);
                    throw transportException;
                }

                return (true, faultyResponse);
            }

            return (false, null);
        }

        /// <summary>
        /// Used to inject faults on channel open
        /// </summary>
        /// <param name="activityId"></param>
        /// <param name="connectionCorrilationId"></param>
        /// <param name="serverUri"></param>
        /// <param name="openingRequest"></param>
        /// <param name="channel"></param>
        public async Task OnChannelOpenAsync(
            Guid activityId,
            Guid connectionCorrilationId,
            Uri serverUri,
            DocumentServiceRequest openingRequest,
            Channel channel)
        {
            FaultInjectionServerErrorRule? serverConnectionDelayRule = this.ruleStore?.FindRntbdServerConnectionDelayRule(
                serverUri,
                openingRequest, 
                activityId);
            this.channelStore.AddChannel(connectionCorrilationId, channel);

            if (serverConnectionDelayRule != null)
            {
                serverConnectionDelayRule.GetDelay();
             
                this.applicationContext.AddRuleExecution(serverConnectionDelayRule.GetId(), activityId);

                DefaultTrace.TraceInformation(
                    "FaultInjection: FaultInjection Rule {0} Inserted {1} duration connection delay for request {2}",
                    serverConnectionDelayRule.GetId(),
                    serverConnectionDelayRule.GetDelay(),
                    activityId);

                TimeSpan connectionDelay = serverConnectionDelayRule.GetDelay();
                await Task.Delay(connectionDelay);
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
        public async Task OnBeforeConnectionWriteAsync(ChannelCallArguments args)
        {
            FaultInjectionServerErrorRule? serverSendDelayRule = this.ruleStore?.FindRntbdServerSendDelayRule(args);
                       
            if (serverSendDelayRule != null)
            {
                this.applicationContext.AddRuleExecution(serverSendDelayRule.GetId(), args.CommonArguments.ActivityId);
                TimeSpan delay = serverSendDelayRule.GetDelay();

                DefaultTrace.TraceInformation(
                    "FaultInjection: FaultInjection Rule {0} Inserted {1} duration send delay for request {2}",
                    serverSendDelayRule.GetId(),
                    delay,
                    args.CommonArguments.ActivityId);

                await Task.Delay(delay);
            }
        }

        /// <summary>
        /// Used to inject faults after connection writes
        /// </summary>
        /// <param name="args"></param>
        public async Task OnAfterConnectionWriteAsync(ChannelCallArguments args)
        {
            FaultInjectionServerErrorRule? serverResponseDelayRule = this.ruleStore?.FindRntbdServerResponseDelayRule(args);

            if (serverResponseDelayRule != null)
            {
                this.applicationContext.AddRuleExecution(serverResponseDelayRule.GetId(), args.CommonArguments.ActivityId);
                TimeSpan delay = serverResponseDelayRule.GetDelay();

                DefaultTrace.TraceInformation(
                    "FaultInjection: FaultInjection Rule {0} Inserted {1} duration response delay for request {2}",
                     serverResponseDelayRule.GetId(),
                     delay,
                     args.CommonArguments.ActivityId);

                await Task.Delay(delay);
            }
        }

        /// <summary>
        /// Gets the fault injection rule id for the given activity id
        /// If multible FaultInjectionRules are applied to the same activity, the first rule applied will be returned
        /// Will return the empty string if no rule is found
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>the fault injection rule id</returns>
        public string GetFaultInjectionRuleId(Guid activityId)
        {
            if (this.applicationContext.TryGetRuleExecutionByActivityId(activityId, out (DateTime, string) execution))
            {
                return execution.Item2;
            }
            return string.Empty;
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

        public async Task<(bool, HttpResponseMessage?)> OnHttpRequestCallAsync(DocumentServiceRequest request)
        {
            HttpResponseMessage faultyResponse;
            FaultInjectionServerErrorRule? serverResponseErrorRule = this.ruleStore?.FindHttpServerResponseErrorRule(request);
            if (serverResponseErrorRule != null)
            {
                this.applicationContext.AddRuleExecution(
                    serverResponseErrorRule.GetId(), 
                    new Guid(request.Headers.Get(ChaosInterceptor.FaultInjectionId)));

                faultyResponse = serverResponseErrorRule.GetInjectedServerError(request);

                DefaultTrace.TraceInformation(
                    "FaultInjection: FaultInjection Rule {0} Inserted error for request with faultInjection request id{1}",
                    serverResponseErrorRule.GetId(),
                    request.Headers.Get(ChaosInterceptor.FaultInjectionId));

                if (serverResponseErrorRule.GetInjectedServerErrorType() == FaultInjectionServerErrorType.Timeout)
                {
                    await Task.Delay(this.requestTimeout);
                }

                return (true, faultyResponse);
            }

            return (false, null);
        }

        public async Task OnBeforeHttpSendAsync(DocumentServiceRequest request)
        {
            FaultInjectionServerErrorRule? serverSendDelayRule = this.ruleStore?.FindHttpServerSendDelayRule(request);

            if (serverSendDelayRule != null)
            {
                this.applicationContext.AddRuleExecution(
                    serverSendDelayRule.GetId(),
                    new Guid(request.Headers.Get(ChaosInterceptor.FaultInjectionId)));
                TimeSpan delay = serverSendDelayRule.GetDelay();

                DefaultTrace.TraceInformation(
                    "FaultInjection: FaultInjection Rule {0} Inserted {1} duration send delay for request with fault injection id {2}",
                    serverSendDelayRule.GetId(),
                    delay,
                    request.Headers.Get(ChaosInterceptor.FaultInjectionId));

                await Task.Delay(delay);
            }
        }

        public async Task OnAfterHttpSendAsync(DocumentServiceRequest request)
        {
            FaultInjectionServerErrorRule? serverResponseDelayRule = this.ruleStore?.FindHttpServerResponseDelayRule(request);

            if (serverResponseDelayRule != null)
            {
                this.applicationContext.AddRuleExecution(
                    serverResponseDelayRule.GetId(),
                    new Guid(request.Headers.Get(ChaosInterceptor.FaultInjectionId)));
                TimeSpan delay = serverResponseDelayRule.GetDelay();

                DefaultTrace.TraceInformation(
                    "FaultInjection: FaultInjection Rule {0} Inserted {1} duration response delay for request with fault injection id {2}",
                    serverResponseDelayRule.GetId(),
                    delay,
                    request.Headers.Get(ChaosInterceptor.FaultInjectionId));

                await Task.Delay(delay);
            }
        }
    }
}