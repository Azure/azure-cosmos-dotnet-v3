//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    /// <summary>  
    /// Interface for injecting faults into the Cosmos DB client operations.  
    /// </summary>  
    public interface IFaultInjector
    {
        /// <summary>  
        /// Gets the chaosInterceptorFactory for client building  
        /// </summary>  
        internal IChaosInterceptorFactory GetChaosInterceptorFactory();
    }
}
