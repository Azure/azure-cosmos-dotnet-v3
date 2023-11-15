//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.FaultInjection.implementataion;
    using Microsoft.Azure.Documents.FaultInjection;

    public class FaultInjector
    {
        private readonly ChaosInterceptor chaosInterceptor;

        public FaultInjector(List<FaultInjectionRule> rules)
        {
            this.chaosInterceptor = new ChaosInterceptor(rules);
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

        internal void ConfigureInterceptor(DocumentClient client, TimeSpan requestTimeout)
        {
            this.chaosInterceptor.ConfigureInterceptor(client, requestTimeout);
        }
        internal IChaosInterceptor GetChaosInterceptor()
        {
            return this.chaosInterceptor;
        }

        //Get Application Context
        internal FaultInjectionApplicationContext GetApplicationContext()
        {
            return this.chaosInterceptor.GetApplicationContext();
        }

    }
}