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
    abstract partial class CosmosInt16 : CosmosNumber, IEquatable<CosmosInt16>
    {
        private sealed class EagerCosmosInt16 : CosmosInt16
        {
            private readonly short number;

            public EagerCosmosInt16(short number)
            {
                this.number = number;
            }

            public override short GetValue()
            {
                return this.number;
            }
        }
    }
}
