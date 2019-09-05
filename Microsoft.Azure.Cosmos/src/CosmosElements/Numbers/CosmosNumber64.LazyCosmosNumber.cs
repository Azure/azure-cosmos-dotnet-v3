//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosNumber64 : CosmosNumber
    {
        private sealed class LazyCosmosNumber64 : CosmosNumber64
        {
            private readonly long MaxSafeInteger = (long)Math.Pow(2, 53 - 1);
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;
            private readonly Lazy<Number64> lazyNumber;

            public LazyCosmosNumber64(
                IJsonNavigator jsonNavigator,
                IJsonNavigatorNode jsonNavigatorNode)
            {
                if (jsonNavigator == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonNavigator)}");
                }

                if (jsonNavigatorNode == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonNavigatorNode)}");
                }

                JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
                if (type != JsonNodeType.Number)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Number} node. Got {type} instead.");
                }

                this.jsonNavigator = jsonNavigator;
                this.jsonNavigatorNode = jsonNavigatorNode;
                this.lazyNumber = new Lazy<Number64>(() =>
                {
                    return this.jsonNavigator.GetNumberValue(this.jsonNavigatorNode);
                });
            }

            public override bool IsFloatingPoint
            {
                get
                {
                    return this.lazyNumber.Value.IsDouble;
                }
            }

            public override bool IsInteger
            {
                get
                {
                    return this.lazyNumber.Value.IsInteger;
                }
            }

            public override double? AsFloatingPoint()
            {
                double? value;
                if (this.IsFloatingPoint)
                {
                    value = Number64.ToDouble(this.lazyNumber.Value);
                }
                else
                {
                    value = null;
                }

                return value;
            }

            public override long? AsInteger()
            {
                long? value;
                if (this.IsFloatingPoint)
                {
                    value = Number64.ToLong(this.lazyNumber.Value);
                }
                else
                {
                    value = null;
                }

                return value;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteNumberValue(this.lazyNumber.Value);
            }
        }
    }
}
