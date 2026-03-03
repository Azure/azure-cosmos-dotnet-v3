//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents.FaultInjection;

    /// <summary>
    /// Manages fault injection rules and provides access to fault injection client options and diagnostics.
    /// </summary>
    public class FaultInjector : IFaultInjector
    {
        private readonly ChaosInterceptorFactory chaosInterceptorFactory;

        public FaultInjector(List<FaultInjectionRule> rules)
        {
            this.chaosInterceptorFactory = new ChaosInterceptorFactory(rules);
        }

        /// <summary>
        /// Configures the provided <see cref="CosmosClientOptions"/> with the fault injection interceptor.
        /// </summary>
        /// <param name="clientOptions">The <see cref="CosmosClientOptions"/> to configure.</param>
        /// <returns>The configured <see cref="CosmosClientOptions"/>.</returns>
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

        /// <summary>
        /// Gets the <see cref="FaultInjectionApplicationContext"/> containing rule execution tracking data.
        /// </summary>
        /// <returns>The <see cref="FaultInjectionApplicationContext"/>, or null if not yet initialized.</returns>
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