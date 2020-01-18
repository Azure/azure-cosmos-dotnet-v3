// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
    internal interface ICosmosNumberVisitor
    {
        void Visit(CosmosFloat32 cosmosFloat32);
    }
}
