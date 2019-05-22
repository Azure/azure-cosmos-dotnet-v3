//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract partial class CosmosFloat64 : CosmosNumber
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
}