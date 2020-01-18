// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal interface ICosmosElementVisitor
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
