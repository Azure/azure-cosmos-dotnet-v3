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
    abstract partial class CosmosInt32 : CosmosNumber
    {
        private sealed class EagerCosmosInt32 : CosmosInt32
        {
            private readonly int number;

            public EagerCosmosInt32(int number)
            {
                this.number = number;
            }

            public override int GetValue()
            {
                return this.number;
            }
        }
    }
}