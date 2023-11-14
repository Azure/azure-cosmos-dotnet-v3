//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using Microsoft.Azure.Cosmos.FaultInjection.implementataion;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Rntbd;
    using System;
    using System.Collections.Generic;
    using System.Reflection.Metadata;
    using System.Text;

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

        internal void ConfigureInterceptor(DocumentClient client)
        {
            //give it the stuff it needs 
            this.chaosInterceptor.ConfigureInterceptor(client);
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