//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosInt16 : CosmosNumber
#else
    internal abstract partial class CosmosInt16 : CosmosNumber
#endif
    {
        private sealed class EagerCosmosInt16 : CosmosInt16
        {
            private readonly short number;

            public EagerCosmosInt16(short number)
            {
                this.number = number;
            }

            protected override short GetValue()
            {
                return this.number;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}