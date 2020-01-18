// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    interface ICosmosElementVisitor<TInput, TResult>
    {
        TResult Visit(CosmosArray cosmosArray, TInput input);
        TResult Visit(CosmosBinary cosmosBinary, TInput input);
        TResult Visit(CosmosBoolean cosmosBoolean, TInput input);
        TResult Visit(CosmosGuid cosmosGuid, TInput input);
        TResult Visit(CosmosNull cosmosNull, TInput input);
        TResult Visit(CosmosNumber cosmosNumber, TInput input);
        TResult Visit(CosmosObject cosmosObject, TInput input);
        TResult Visit(CosmosString cosmosString, TInput input);
    }
}
