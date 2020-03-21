//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1602 // Enumeration items should be documented
    public
#else
    internal
#endif
    enum CosmosElementType
    {
        Array,
        Boolean,
        Null,
        Number,
        Object,
        String,
        Guid,
        Binary,
    }
}