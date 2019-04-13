//-----------------------------------------------------------------------
// <copyright file="CosmosNull.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements.Patchable;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class CosmosNull : CosmosElement
    {
        private static readonly CosmosNull Singleton = new CosmosNull();

        private CosmosNull()
            : base(CosmosElementType.Null)
        {
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteNullValue();
        }

        public override PatchableCosmosElement ToPatchable()
        {
            return PatchableCosmosNull.Create();
        }

        public static CosmosNull Create()
        {
            return CosmosNull.Singleton;
        }
    }
}
