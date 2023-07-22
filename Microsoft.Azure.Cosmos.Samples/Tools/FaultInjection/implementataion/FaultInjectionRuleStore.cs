//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Tracing;

    public class FaultInjectionRuleStore
    {
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverResponseDelayRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverResponseErrorRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionServerErrorRule, byte> serverConnectionDelayRuleSet = new ConcurrentDictionary<FaultInjectionServerErrorRule, byte>();
        private readonly ConcurrentDictionary<FaultInjectionConnectionErrorRule, byte> connectionErrorRuleSet = new ConcurrentDictionary<FaultInjectionConnectionErrorRule, byte>();

        private readonly FaultInjectionRuleProcessor ruleProcessor;

        public FaultInjectionRuleStore(DocumentClient client)
        {
            _= client ?? throw new ArgumentNullException(nameof(client));

            this.ruleProcessor = new FaultInjectionRuleProcessor(
                connectionMode: client.ConnectionPolicy.ConnectionMode,
                collectionCache: client.GetCollectionCacheAsync(NoOpTrace.Singleton).Result,
                globalEndpointManager: client.GlobalEndpointManager,
                addressResolver: client.AddressResolver,
                retryOptions: client.ConnectionPolicy.RetryOptions,
                routingMapProvider: client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton).Result);
        }

        public IFaultInjectionRuleInternal ConfigureFaultInjectionRule(FaultInjectionRule rule, string containerUri)
        {
            _ = rule ?? throw new ArgumentNullException(nameof(rule));
            _ = string.IsNullOrEmpty(containerUri) ? throw new ArgumentNullException($"{nameof(containerUri)} cannot be null or empty") : containerUri;

            IFaultInjectionRuleInternal effectiveRule = this.ruleProcessor.ProcessFaultInjectionRule(rule, containerUri);
            
            if (effectiveRule.GetType() == typeof(FaultInjectionConnectionErrorRule))
            {
                this.connectionErrorRuleSet.TryAdd((FaultInjectionConnectionErrorRule)effectiveRule, 0);
            }
            else if (effectiveRule.GetType() == typeof(FaultInjectionServerErrorRule))
            {
                FaultInjectionServerErrorRule serverErrorRule = (FaultInjectionServerErrorRule)effectiveRule;

                switch (serverErrorRule.GetResult().GetServerErrorType())
                {
                    case FaultInjectionServerErrorType.RESPONSE_DELAY:
                        this.serverResponseDelayRuleSet.TryAdd(serverErrorRule, 0);
                        break;
                    case FaultInjectionServerErrorType.CONNECTION_DELAY:
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
                if (rule.GetConnectionType() == FaultInjectionConnectionType.DIRECT_MODE
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
                if (rule.GetConnectionType() == FaultInjectionConnectionType.DIRECT_MODE
                    && rule.IsApplicable(args))
                {
                    return rule;
                }
            }

            return null;
        }

        public FaultInjectionServerErrorRule? FindRntbdServerConnectionDelayRule(
            Guid activityId,
            string callUri, 
            DocumentServiceRequest request)
        {
            foreach (FaultInjectionServerErrorRule rule in this.serverConnectionDelayRuleSet.Keys)
            {
                if (rule.GetConnectionType() == FaultInjectionConnectionType.DIRECT_MODE
                    && rule.IsApplicable(
                        activityId,
                        callUri,
                        request))
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
    }
}
