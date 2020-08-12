// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 226

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
    interface ICosmosNumberVisitor<TResult>
    {
        TResult Visit(CosmosNumber64 cosmosNumber64);
        TResult Visit(CosmosInt8 cosmosInt8);
        TResult Visit(CosmosInt16 cosmosInt16);
        TResult Visit(CosmosInt32 cosmosInt32);
        TResult Visit(CosmosInt64 cosmosInt64);
        TResult Visit(CosmosUInt32 cosmosUInt32);
        TResult Visit(CosmosFloat32 cosmosFloat32);
        TResult Visit(CosmosFloat64 cosmosFloat64);
    }
}
