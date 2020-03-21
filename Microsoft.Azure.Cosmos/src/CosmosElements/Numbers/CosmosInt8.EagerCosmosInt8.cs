//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosInt8 : CosmosNumber
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