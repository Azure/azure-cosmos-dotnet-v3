//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract partial class CosmosInt8 : CosmosNumber
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
}