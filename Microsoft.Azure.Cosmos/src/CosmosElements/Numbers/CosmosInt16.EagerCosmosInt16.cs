//-----------------------------------------------------------------------
// <copyright file="CosmosInt16.EagerCosmosInt16.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract partial class CosmosInt16 : CosmosNumber
    {
        private sealed class EagerCosmosInt16 : CosmosInt16
        {
            private readonly short number;

            public EagerCosmosInt16(short number)
            {
                this.number = number;
            }

            protected override short GetValue()
            {
                return this.number;
            }
        }
    }
}