//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;

    internal class FaultInjectionRuleStore
    {
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverResponseDelayRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverSendDelayRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverResponseErrorRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverConnectionDelayRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionConnectionErrorRule, byte> connectionErrorRuleSet = new ConcurrentDictionary<FaultInjectionConnectionErrorRule, byte>();

        private readonly FaultInjectionRuleProcessor ruleProcessor;

        public static async Task<FaultInjectionRuleStore> CreateAsync(
            DocumentClient client,
            FaultInjectionApplicationContext applicationContext)
        {
            CollectionCache collectionCache = await client.GetCollectionCacheAsync(NoOpTrace.Singleton);
            IRoutingMapProvider routingMapProvider = await client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            return new FaultInjectionRuleStore(
                connectionMode: client.ConnectionPolicy.ConnectionMode,
                collectionCache: collectionCache,
                globalEndpointManager: client.GlobalEndpointManager,
                addressResolver: client.AddressResolver,
                retryPolicy: client.ResetSessionTokenRetryPolicy.GetRequestPolicy,
                routingMapProvider: routingMapProvider,
                applicationContext: applicationContext);
        }

        private FaultInjectionRuleStore(
            ConnectionMode connectionMode,
            CollectionCache collectionCache,
            GlobalEndpointManager globalEndpointManager,
            GlobalAddressResolver addressResolver,
            Func<IRetryPolicy> retryPolicy,
            IRoutingMapProvider routingMapProvider,
            FaultInjectionApplicationContext applicationContext)
        {
            this.ruleProcessor = new FaultInjectionRuleProcessor(
                    connectionMode: connectionMode,
                    collectionCache: collectionCache,
                    globalEndpointManager: globalEndpointManager,
                    addressResolver: addressResolver,
                    retryPolicy: retryPolicy,
                    routingMapProvider: routingMapProvider,
                    applicationContext: applicationContext);
        }

        public async Task<IFaultInjectionRuleInternal> ConfigureFaultInjectionRuleAsync(FaultInjectionRule rule)
        {
            _ = rule ?? throw new ArgumentNullException(nameof(rule));

            IFaultInjectionRuleInternal effectiveRule = await this.ruleProcessor.ProcessFaultInjectionRule(rule);
            rule.SetEffectiveFaultInjectionRule(effectiveRule);

            if (effectiveRule.GetType() == typeof(FaultInjectionConnectionErrorRule))
            {
                this.connectionErrorRuleSet.TryAdd((FaultInjectionConnectionErrorRule)effectiveRule, 0);
            }
            else if (effectiveRule.GetType() == typeof(FaultInjectionServerErrorRule))
            {
                FaultInjectionServerErrorRule serverErrorRule = (FaultInjectionServerErrorRule)effectiveRule;

                switch (serverErrorRule.GetResult().GetServerErrorType())
                {
                    case FaultInjectionServerErrorType.ResponseDelay:
                        this.serverResponseDelayRuleSet.TryAdd(serverErrorRule, 0);
                        break;
                    case FaultInjectionServerErrorType.SendDelay:
                        this.serverSendDelayRuleSet.TryAdd(serverErrorRule, 0);
                        break;
                    case FaultInjectionServerErrorType.ConnectionDelay:
                        this.serverConnectionDelayRuleSet.TryAdd(serverErrorRule, 0);
                        break;
                    default:
                        this.serverResponseErrorRuleSet.TryAdd(serverErrorRule, 0);
                        break;
                }
            }

            return effectiveRule;
        }

        public FaultInjectionServerErrorRule? FindRntbdServerResponseErrorRule(ChannelCallArguments args)
        {
            foreach (FaultInjectionServerErrorRule rule in this.serverResponseErrorRuleSet.Keys)
            {
                if ((rule.GetConnectionType() == FaultInjectionConnectionType.Direct
                    || rule.GetConnectionType() == FaultInjectionConnectionType.All)
                    && rule.IsApplicable(args))
                {
                    return rule;
                }
            }

            return null;
        }

        public FaultInjectionServerErrorRule? FindRntbdServerResponseDelayRule(ChannelCallArguments args)
        {
            foreach (FaultInjectionServerErrorRule rule in this.serverResponseDelayRuleSet.Keys)
            {
                if ((rule.GetConnectionType() == FaultInjectionConnectionType.Direct 
                    || rule.GetConnectionType() == FaultInjectionConnectionType.All)
                    && rule.IsApplicable(args))
                {
                    return rule;
                }
            }

            return null;
        }

        public FaultInjectionServerErrorRule? FindRntbdServerSendDelayRule(ChannelCallArguments args)
        {
            foreach (FaultInjectionServerErrorRule rule in this.serverSendDelayRuleSet.Keys)
            {
                if ((rule.GetConnectionType() == FaultInjectionConnectionType.Direct
                    || rule.GetConnectionType() == FaultInjectionConnectionType.All)
                    && rule.IsApplicable(args))
                {
                    return rule;
                }
            }

            return null;
        }


        public FaultInjectionServerErrorRule? FindRntbdServerConnectionDelayRule(
            Uri callUri, 
            DocumentServiceRequest request,
            Guid activityId)
        {
            foreach (FaultInjectionServerErrorRule rule in this.serverConnectionDelayRuleSet.Keys)
            {
                if ((rule.GetConnectionType() == FaultInjectionConnectionType.Direct 
                    || rule.GetConnectionType() == FaultInjectionConnectionType.All)
                    && rule.IsApplicable(
                        callUri,
                        request,
                        activityId))
                {
                    return rule;
                }
            }

            return null;
        }

        public FaultInjectionServerErrorRule? FindHttpServerResponseErrorRule(DocumentServiceRequest dsr)
        {
            foreach (FaultInjectionServerErrorRule rule in this.serverResponseErrorRuleSet.Keys)
            {
                if ((rule.GetConnectionType() == FaultInjectionConnectionType.Gateway
                    || rule.GetConnectionType() == FaultInjectionConnectionType.All)
                    && rule.IsApplicable(dsr))
                {
                    return rule;
                }
            }

            return null;
        }

        public FaultInjectionServerErrorRule? FindHttpServerResponseDelayRule(DocumentServiceRequest dsr)
        {
            foreach (FaultInjectionServerErrorRule rule in this.serverResponseDelayRuleSet.Keys)
            {
                if ((rule.GetConnectionType() == FaultInjectionConnectionType.Gateway
                    || rule.GetConnectionType() == FaultInjectionConnectionType.All)
                    && rule.IsApplicable(dsr))
                {
                    return rule;
                }
            }

            return null;
        }

        public FaultInjectionServerErrorRule? FindHttpServerSendDelayRule(DocumentServiceRequest dsr)
        {
            foreach (FaultInjectionServerErrorRule rule in this.serverSendDelayRuleSet.Keys)
            {
                if ((rule.GetConnectionType() == FaultInjectionConnectionType.Gateway
                    || rule.GetConnectionType() == FaultInjectionConnectionType.All)
                    && rule.IsApplicable(dsr))
                {
                    return rule;
                }
            }

            return null;
        }

        public bool ContainsRule(FaultInjectionConnectionErrorRule rule)
        {
            return this.connectionErrorRuleSet.ContainsKey(rule);
        }

        public bool RemoveRule(FaultInjectionConnectionErrorRule rule)
        {
            return this.connectionErrorRuleSet.Remove(rule, out byte _);
        }

        internal FaultInjectionRuleProcessor GetRuleProcessor()
        {
            return this.ruleProcessor;
        }
    }
}
