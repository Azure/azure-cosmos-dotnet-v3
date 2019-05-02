//-----------------------------------------------------------------------
// <copyright file="CosmosString.EagerCosmosGuid.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;

    internal abstract partial class CosmosGuid : CosmosElement
    {
        private sealed class EagerCosmosGuid : CosmosGuid
        {
            public EagerCosmosGuid(Guid value)
            {
                this.Value = value;
            }

            public override Guid Value
            {
                get;
            }
        }
    }
}