//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using Microsoft.Azure.Documents.FaultInjection;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class FaultInjectionProvider : IFaultInjectorProvider
    {
        private readonly FaultInjectionRuleStore ruleStore;
        private readonly RntbdServerErrorInjector serverErrorInjector;
        private readonly string containerUri;

        //private RntbdConnectionErrorInjector connectionErrorInjector;

        public FaultInjectorProvider(string containerLink, DocumentClient client)
        {
            _ = string.IsNullOrEmpty(containerLink) ? throw new ArgumentNullException($"nameof(containerLink) cannot be null or empty") : containerLink;
            _ = client ?? throw new ArgumentNullException(nameof(client));

            this.containerUri = containerLink;
            this.ruleStore = new FaultInjectionRuleStore(client);
            this.serverErrorInjector = new RntbdServerErrorInjector(this.ruleStore);
        }

        //public void ConfigureFaultInjectionRules(List<FaultInjectionRule> rules)
        //{
        //    rules.Select(
        //        rule =>
        //        {
        //            IFaultInjectionRuleInternal effectiveRule = this.ruleStore.ConfigureFaultInjectionRule(rule, this.containerUri);

        //            //this.conectionErrorInjector.Accept(effectiveRule);
        //        })
        //}

        public IRntbdServerErrorInjector GetServerErrorInjector()
        {
            return this.serverErrorInjector;
        }

    }
}