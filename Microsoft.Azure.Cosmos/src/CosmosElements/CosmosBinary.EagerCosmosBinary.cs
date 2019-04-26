//-----------------------------------------------------------------------
// <copyright file="CosmosBinary.EagerCosmosBinary.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosBinary : CosmosElement
    {
        private sealed class EagerCosmosBinary : CosmosBinary
        {
            public EagerCosmosBinary(byte[] value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException($"{nameof(value)}");
                }

                this.Value = value;
            }

            public override byte[] Value
            {
                get;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteBinaryValue(this.Value);
            }
        }
    }
}