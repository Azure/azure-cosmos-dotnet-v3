//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosNumber64 : CosmosNumber
#else
    internal abstract partial class CosmosNumber64 : CosmosNumber
#endif
    {
        private sealed class EagerCosmosNumber64 : CosmosNumber64
        {
            private readonly Number64 number;

            public EagerCosmosNumber64(Number64 number)
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
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}