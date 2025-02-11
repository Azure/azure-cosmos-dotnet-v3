//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents.FaultInjection;

    public class FaultInjector : IFaultInjector
    {
        private readonly ChaosInterceptorFactory chaosInterceptorFactory;

        public FaultInjector(List<FaultInjectionRule> rules)
        {
            this.chaosInterceptorFactory = new ChaosInterceptorFactory(rules);
        }

        public CosmosClientOptions GetFaultInjectionClientOptions(CosmosClientOptions clientOptions)
        {
            clientOptions.ChaosInterceptorFactory = this.chaosInterceptorFactory;
            return clientOptions;
        }       

        /// <summary>
        /// Gets the fault injection rule id for the given activity id
        /// If multible FaultInjectionRules are applied to the same activity, the first rule applied will be returned
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>the fault injection rule id</returns>
        public string? GetFaultInjectionRuleId(Guid activityId)
        {
            return this.chaosInterceptorFactory.ChaosInterceptor?.GetFaultInjectionRuleId(activityId);
        }

        //Get Application Context
        public FaultInjectionApplicationContext? GetApplicationContext()
        {
            return this.chaosInterceptorFactory.ChaosInterceptor?.GetApplicationContext();
        }

        internal IChaosInterceptor? GetChaosInterceptor()
        {
            return this.chaosInterceptorFactory.ChaosInterceptor;
        }

        internal IChaosInterceptorFactory GetChaosInterceptorFactory()
        {
            return this.chaosInterceptorFactory;
        }

        IChaosInterceptorFactory IFaultInjector.GetChaosInterceptorFactory()
        {
            return this.chaosInterceptorFactory;
        }
    }
}