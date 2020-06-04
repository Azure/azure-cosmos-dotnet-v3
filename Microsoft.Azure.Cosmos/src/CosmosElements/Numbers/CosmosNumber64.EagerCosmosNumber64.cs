//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 142

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
    using System;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosNumber64 : CosmosNumber, IEquatable<CosmosNumber64>
    {
        private sealed class EagerCosmosNumber64 : CosmosNumber64
        {
            private readonly Number64 number;

            public EagerCosmosNumber64(Number64 number)
            {
                this.number = number;
            }

            public override Number64 GetValue()
            {
                return this.number;
            }
        }
    }
}
