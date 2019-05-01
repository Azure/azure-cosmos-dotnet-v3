//-----------------------------------------------------------------------
// <copyright file="CosmosUInt32.EagerCosmosUInt32.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract partial class CosmosUInt32 : CosmosNumber
    {
        private sealed class EagerCosmosUInt32 : CosmosUInt32
        {
            private readonly uint number;

            public EagerCosmosUInt32(uint number)
            {
                this.number = number;
            }

            protected override uint GetValue()
            {
                return this.number;
            }
        }
    }
}