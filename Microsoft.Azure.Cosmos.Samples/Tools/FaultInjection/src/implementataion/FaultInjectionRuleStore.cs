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
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;

    internal class FaultInjectionRuleStore
    {
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverResponseDelayRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverResponseErrorRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverConnectionDelayRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionConnectionErrorRule, byte> connectionErrorRuleSet = new ConcurrentDictionary<FaultInjectionConnectionErrorRule, byte>();

        private readonly FaultInjectionRuleProcessor ruleProcessor;

        public FaultInjectionRuleStore(DocumentClient client, FaultInjectionApplicationContext applicationContext)
        {
            _= client ?? throw new ArgumentNullException(nameof(client));

            this.ruleProcessor = new FaultInjectionRuleProcessor(
                connectionMode: client.ConnectionPolicy.ConnectionMode,
                collectionCache: client.GetCollectionCacheAsync(NoOpTrace.Singleton).Result,
                globalEndpointManager: client.GlobalEndpointManager,
                addressResolver: client.AddressResolver,
                retryPolicy: client.ResetSessionTokenRetryPolicy.GetRequestPolicy,
                routingMapProvider: client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton).Result,
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
                if (rule.GetConnectionType() == FaultInjectionConnectionType.Direct
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
                if (rule.GetConnectionType() == FaultInjectionConnectionType.Direct
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
                if (rule.GetConnectionType() == FaultInjectionConnectionType.Direct
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
