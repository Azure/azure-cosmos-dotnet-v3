//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosInt8 : CosmosNumber
#else
    internal abstract partial class CosmosInt8 : CosmosNumber
#endif
    {
        private sealed class EagerCosmosInt8 : CosmosInt8
        {
            private readonly sbyte number;

            public EagerCosmosInt8(sbyte number)
            {
                this.number = number;
            }

            protected override sbyte GetValue()
            {
                return this.number;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}