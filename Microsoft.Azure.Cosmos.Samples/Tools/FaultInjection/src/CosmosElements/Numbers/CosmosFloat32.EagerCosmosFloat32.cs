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
    abstract partial class CosmosFloat32 : CosmosNumber, IEquatable<CosmosFloat32>, IComparable<CosmosFloat32>
    {
        private sealed class EagerCosmosFloat32 : CosmosFloat32
        {
            private readonly float number;

            public EagerCosmosFloat32(float number)
            {
                this.number = number;
            }

            public override float GetValue()
            {
                return this.number;
            }
        }
    }
}
