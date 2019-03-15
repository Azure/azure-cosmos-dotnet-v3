//-----------------------------------------------------------------------
// <copyright file="CosmosNumber.EagerCosmosNumber.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosNumber : CosmosElement
    {
        private sealed class EagerCosmosNumber : CosmosNumber
        {
            private readonly Number64 number;

            public EagerCosmosNumber(Number64 number)
            {
                this.number = number;
            }

            public override bool IsFloatingPoint
            {
                get
                {
                    return this.number.IsDouble;
                }
            }

            public override bool IsInteger
            {
                get
                {
                    return this.number.IsInteger;
                }
            }

            public override double? AsFloatingPoint()
            {
                return Number64.ToDouble(this.number);
            }

            public override long? AsInteger()
            {
                return Number64.ToLong(this.number);
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteNumberValue(this.AsFloatingPoint().Value);
            }
        }
    }
}