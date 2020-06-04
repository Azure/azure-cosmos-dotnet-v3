// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 197

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
#nullable enable

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    interface ICosmosNumberVisitor
    {
        void Visit(CosmosNumber64 cosmosNumber64);
        void Visit(CosmosInt8 cosmosInt8);
        void Visit(CosmosInt16 cosmosInt16);
        void Visit(CosmosInt32 cosmosInt32);
        void Visit(CosmosInt64 cosmosInt64);
        void Visit(CosmosUInt32 cosmosUInt32);
        void Visit(CosmosFloat32 cosmosFloat32);
        void Visit(CosmosFloat64 cosmosFloat64);
    }
}
