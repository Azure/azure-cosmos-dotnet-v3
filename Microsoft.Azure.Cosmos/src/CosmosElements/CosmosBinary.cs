//-----------------------------------------------------------------------
// <copyright file="CosmosBinary.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosBinary : CosmosElement
    {
        protected CosmosBinary()
            : base(CosmosElementType.Binary)
        {
        }

        public abstract IReadOnlyList<byte> Value
        {
            get;
        }

        public static CosmosBinary Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosBinary(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosBinary Create(IReadOnlyList<byte> value)
        {
            return new EagerCosmosBinary(value);
        }
    }
}
