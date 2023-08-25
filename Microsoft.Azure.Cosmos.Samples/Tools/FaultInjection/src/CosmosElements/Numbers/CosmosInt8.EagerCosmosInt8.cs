//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 101

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
#nullable enable

    using System;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosInt8 : CosmosNumber, IEquatable<CosmosInt8>, IComparable<CosmosInt8>
    {
        private sealed class EagerCosmosInt8 : CosmosInt8
        {
            private readonly sbyte number;

            public EagerCosmosInt8(sbyte number)
            {
                this.number = number;
            }

            public override sbyte GetValue()
            {
                return this.number;
            }
        }
    }
}
