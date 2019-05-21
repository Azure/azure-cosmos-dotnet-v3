//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract partial class CosmosInt64 : CosmosNumber
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
}