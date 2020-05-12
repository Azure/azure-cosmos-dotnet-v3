//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 107

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosFloat32 : CosmosNumber
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
