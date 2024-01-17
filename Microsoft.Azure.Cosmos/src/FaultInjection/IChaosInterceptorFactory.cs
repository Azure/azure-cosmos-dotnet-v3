//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents.FaultInjection;

    /// <summary>
    /// This interface is used by the fault injection library to create an instance of IChaosInterceptor
    /// This allows the fault injection library to intercept requests and inject faults in the request process
    /// </summary>
    internal interface IChaosInterceptorFactory
    {
        /// <summary>
        /// Creates the IChaosInterceptor interceptor that will be used to inject fault injection rules. 
        /// </summary>
        /// <param name="documentClient"></param>
        public IChaosInterceptor CreateInterceptor(DocumentClient documentClient);
    }
}