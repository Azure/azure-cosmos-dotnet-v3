// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal interface ICosmosElementVisitor<TResult>
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
