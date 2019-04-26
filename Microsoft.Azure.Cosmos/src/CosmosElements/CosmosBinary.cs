//-----------------------------------------------------------------------
// <copyright file="CosmosBinary.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
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

        public int CompareTo(CosmosBinary other)
        {
            if (other == null)
            {
                return 1;
            }

            int minLength = Math.Min(this.Value.Length, other.Value.Length);
            for (int i = 0; i < minLength; i++)
            {
                int cmp = this.Value[i].CompareTo(other.Value[i]);
                if (cmp != 0)
                {
                    return cmp;
                }
            }

            return this.Value.Length.CompareTo(other.Value.Length);
        }
    }
}
