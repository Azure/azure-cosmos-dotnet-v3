//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents.FaultInjection;

    public class FaultInjector
    {
        private readonly ChaosInterceptor chaosInterceptor;

        public FaultInjector(List<FaultInjectionRule> rules)
        {
            this.chaosInterceptor = new ChaosInterceptor(rules);
        }

        public CosmosClientOptions GetFaultInjectionClientOptions(CosmosClientOptions clientOptions)
        {
            clientOptions.ChaosInterceptor = this.chaosInterceptor;
            return clientOptions;
        }       

        /// <summary>
        /// Gets the fault injection rule id for the given activity id
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>the fault injection rule id</returns>
        public string GetFaultInjectionRuleId(Guid activityId)
        {
            return this.chaosInterceptor.GetFaultInjectionRuleId(activityId);
        }

        //Get Application Context
        public FaultInjectionApplicationContext GetApplicationContext()
        {
            return this.chaosInterceptor.GetApplicationContext();
        }

        internal IChaosInterceptor GetChaosInterceptor()
        {
            return this.chaosInterceptor;
        }
    }
}