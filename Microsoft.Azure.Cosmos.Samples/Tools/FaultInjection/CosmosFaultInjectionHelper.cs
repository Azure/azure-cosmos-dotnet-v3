//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;

    /// <summary>
    /// Cosmos Fault Injection Helper
    /// </summary>
    public class CosmosFaultInjectionHelper
    {
        /// <summary>
        /// Configure Fault Injection Rules
        /// <param name="container"> the container.</param>
        /// <param name="rules">the fault injection rules.</param>
        /// </summary>
        public static Task<> ConfigureFaultInjectionRules(Container container, List<FaultInjectionRule> rules)
        {
            FaultInjectionProvider faultInjectionProvider = new FaultInjectionProvider();
        }
    }
}
