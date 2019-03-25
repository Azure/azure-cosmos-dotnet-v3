//-----------------------------------------------------------------------
// <copyright file="CosmosString.EagerCosmosString.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosString : CosmosElement
    {
        private sealed class EagerCosmosString : CosmosString
        {
            public EagerCosmosString(string value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException($"{nameof(value)}");
                }

                this.Value = value;
            }

            public override string Value
            {
                get;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteStringValue(this.Value);
            }
        }
    }
}