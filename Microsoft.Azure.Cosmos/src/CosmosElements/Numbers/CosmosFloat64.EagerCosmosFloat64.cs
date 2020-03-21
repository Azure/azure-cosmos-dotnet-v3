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
    abstract partial class CosmosFloat64 : CosmosNumber
    {
        private sealed class EagerCosmosFloat64 : CosmosFloat64
        {
            private readonly double number;

            public EagerCosmosFloat64(double number)
            {
                this.number = number;
            }

            public override double GetValue()
            {
                return this.number;
            }
        }
    }
}