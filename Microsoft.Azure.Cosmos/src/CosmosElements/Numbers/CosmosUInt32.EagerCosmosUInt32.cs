//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosUInt32 : CosmosNumber
#else
    internal abstract partial class CosmosUInt32 : CosmosNumber
#endif
    {
        private sealed class EagerCosmosUInt32 : CosmosUInt32
        {
            private readonly uint number;

            public EagerCosmosUInt32(uint number)
            {
                this.number = number;
            }

            protected override uint GetValue()
            {
                return this.number;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}