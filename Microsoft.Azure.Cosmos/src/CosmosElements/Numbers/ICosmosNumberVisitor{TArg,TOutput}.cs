// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 224

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    interface ICosmosNumberVisitor<TArg, TOutput>
    {
        TOutput Visit(CosmosNumber64 cosmosNumber64, TArg input);
        TOutput Visit(CosmosInt8 cosmosInt8, TArg input);
        TOutput Visit(CosmosInt16 cosmosInt16, TArg input);
        TOutput Visit(CosmosInt32 cosmosInt32, TArg input);
        TOutput Visit(CosmosInt64 cosmosInt64, TArg input);
        TOutput Visit(CosmosUInt32 cosmosUInt32, TArg input);
        TOutput Visit(CosmosFloat32 cosmosFloat32, TArg input);
        TOutput Visit(CosmosFloat64 cosmosFloat64, TArg input);
    }
}
