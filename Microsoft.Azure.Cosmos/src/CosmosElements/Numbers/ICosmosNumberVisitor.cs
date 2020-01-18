// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
    internal interface ICosmosNumberVisitor
    {
        void Visit(CosmosFloat32 cosmosFloat32);
        void Visit(CosmosFloat64 cosmosFloat64);
        void Visit(CosmosInt16 cosmosInt16);
        void Visit(CosmosInt32 cosmosInt32);
        void Visit(CosmosInt64 cosmosInt64);
        void Visit(CosmosInt8 cosmosInt8);
        void Visit(CosmosNumber64 cosmosNumber64);
        void Visit(CosmosUInt32 cosmosUInt32);
    }
}
