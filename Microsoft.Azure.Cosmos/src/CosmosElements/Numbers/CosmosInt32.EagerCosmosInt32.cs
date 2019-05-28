//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract partial class CosmosInt32 : CosmosNumber
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
}