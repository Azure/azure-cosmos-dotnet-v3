// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    interface ICosmosElementVisitor
    {
        void Visit(CosmosArray cosmosArray);
        void Visit(CosmosBinary cosmosBinary);
        void Visit(CosmosBoolean cosmosBoolean);
        void Visit(CosmosGuid cosmosGuid);
        void Visit(CosmosNull cosmosNull);
        void Visit(CosmosNumber cosmosNumber);
        void Visit(CosmosObject cosmosObject);
        void Visit(CosmosString cosmosString);
    }
}
