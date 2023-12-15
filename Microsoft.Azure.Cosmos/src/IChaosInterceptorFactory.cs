//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents.FaultInjection;

    internal interface IChaosInterceptorFactory
    {
        public IChaosInterceptor CreateInterceptor(DocumentClient documentClient);
    }
}
