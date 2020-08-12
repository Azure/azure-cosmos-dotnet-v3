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
    interface ICosmosElementVisitor<TArg, TResult>
    {
        TResult Visit(CosmosArray cosmosArray, TArg input);
        TResult Visit(CosmosBinary cosmosBinary, TArg input);
        TResult Visit(CosmosBoolean cosmosBoolean, TArg input);
        TResult Visit(CosmosGuid cosmosGuid, TArg input);
        TResult Visit(CosmosNull cosmosNull, TArg input);
        TResult Visit(CosmosNumber cosmosNumber, TArg input);
        TResult Visit(CosmosObject cosmosObject, TArg input);
        TResult Visit(CosmosString cosmosString, TArg input);
    }
}
