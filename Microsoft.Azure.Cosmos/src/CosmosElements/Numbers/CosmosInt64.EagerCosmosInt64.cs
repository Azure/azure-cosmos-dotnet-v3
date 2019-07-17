//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosInt64 : CosmosNumber
#else
    internal abstract partial class CosmosInt64 : CosmosNumber
#endif
    {
        private sealed class EagerCosmosInt64 : CosmosInt64
        {
            private readonly long number;

            public EagerCosmosInt64(long number)
            {
                this.number = number;
            }

            protected override long GetValue()
            {
                return this.number;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}