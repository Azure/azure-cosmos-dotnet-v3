//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class CosmosNull : CosmosElement
    {
        private static readonly CosmosNull Singleton = new CosmosNull();

        private CosmosNull()
            : base(CosmosElementType.Null)
        {
        }

        public static CosmosNull Create()
        {
            return CosmosNull.Singleton;
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteNullValue();
        }
    }
}
