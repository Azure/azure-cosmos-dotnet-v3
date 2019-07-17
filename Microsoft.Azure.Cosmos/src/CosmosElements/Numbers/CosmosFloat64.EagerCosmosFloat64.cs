//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosFloat64 : CosmosNumber
#else
    internal abstract partial class CosmosFloat64 : CosmosNumber
#endif
    {
        private sealed class EagerCosmosFloat64 : CosmosFloat64
        {
            private readonly double number;

            public EagerCosmosFloat64(double number)
            {
                this.number = number;
            }

            protected override double GetValue()
            {
                return this.number;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}