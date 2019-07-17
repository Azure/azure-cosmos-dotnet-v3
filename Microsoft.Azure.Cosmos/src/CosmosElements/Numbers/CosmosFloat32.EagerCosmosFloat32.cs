//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosFloat32 : CosmosNumber
#else
    internal abstract partial class CosmosFloat32 : CosmosNumber
#endif
    {
        private sealed class EagerCosmosFloat32 : CosmosFloat32
        {
            private readonly float number;

            public EagerCosmosFloat32(float number)
            {
                this.number = number;
            }

            protected override float GetValue()
            {
                return this.number;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}