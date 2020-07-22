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
    interface ICosmosElementVisitor<TResult>
    {
        TResult Visit(CosmosArray cosmosArray);
        TResult Visit(CosmosBinary cosmosBinary);
        TResult Visit(CosmosBoolean cosmosBoolean);
        TResult Visit(CosmosGuid cosmosGuid);
        TResult Visit(CosmosNull cosmosNull);
        TResult Visit(CosmosNumber cosmosNumber);
        TResult Visit(CosmosObject cosmosObject);
        TResult Visit(CosmosString cosmosString);
    }
}
