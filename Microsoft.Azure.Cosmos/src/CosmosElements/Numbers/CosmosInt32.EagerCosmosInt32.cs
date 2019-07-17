//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosInt32 : CosmosNumber
#else
    internal abstract partial class CosmosInt32 : CosmosNumber
#endif
    {
        private sealed class EagerCosmosInt32 : CosmosInt32
        {
            private readonly int number;

            public EagerCosmosInt32(int number)
            {
                this.number = number;
            }

            protected override int GetValue()
            {
                return this.number;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}