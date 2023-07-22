//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Rntbd;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class FaultInjectionProvider : IFaultInjectorProvider
    {
        private readonly FaultInjectionRuleStore ruleStore;
        private readonly RntbdServerErrorInjector serverErrorInjector;
        private RntbdConnectionErrorInjector? connectionErrorInjector;
        private readonly string containerUri;

        public FaultInjectorProvider(string containerLink, DocumentClient client)
        {
            _ = string.IsNullOrEmpty(containerLink) ? throw new ArgumentNullException($"nameof(containerLink) cannot be null or empty") : containerLink;
            _ = client ?? throw new ArgumentNullException(nameof(client));

            this.containerUri = containerLink;
            this.ruleStore = new FaultInjectionRuleStore(client);
            this.serverErrorInjector = new RntbdServerErrorInjector(this.ruleStore);
            this.connectionErrorInjector = null;
        }

        public void ConfigureFaultInjectionRules(List<FaultInjectionRule> rules)
        {
            rules.ForEach(
                rule =>
                {
                    IFaultInjectionRuleInternal effectiveRule = this.ruleStore.ConfigureFaultInjectionRule(rule, this.containerUri);
                    this.connectionErrorInjector?.Accept(effectiveRule);
                });
        }

        public IRntbdServerErrorInjector GetRntbdServerErrorInjector()
        {
            return this.serverErrorInjector;
        }

        public void RegisterConnectionErrorInjector(ChannelDictionary channelDictonary)
        {
            this.connectionErrorInjector = new RntbdConnectionErrorInjector(this.ruleStore, channelDictonary);
        }

    }
}