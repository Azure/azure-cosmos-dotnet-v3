//-----------------------------------------------------------------------
// <copyright file="CosmosBinary.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosBinary : CosmosElement
    {
        protected CosmosBinary()
            : base(CosmosElementType.Binary)
        {
        }

        public abstract byte[] Value
        {
            get;
        }

        public static CosmosBinary Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosBinary(jsonNavigator, jsonNavigatorNode);
        }

        public new static CosmosBinary Create(byte[] value)
        {
            return new EagerCosmosBinary(value);
        }
    }
}
