//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosNumber64 : CosmosNumber
    {
        private sealed class LazyCosmosNumber64 : CosmosNumber64
        {
            private readonly long MaxSafeInteger = (long)Math.Pow(2, 53 - 1);
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;

            // TODO: replace this with Number64 when the time comes.
            private readonly Lazy<double> lazyNumber;

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
                this.lazyNumber = new Lazy<double>(() =>
                {
                    return this.jsonNavigator.GetNumberValue(this.jsonNavigatorNode);
                });
            }

            public override bool IsFloatingPoint
            {
                get
                {
                    return !this.IsInteger;
                }
            }

            public override bool IsInteger
            {
                get
                {
                    return this.lazyNumber.Value % 1 == 0 && this.lazyNumber.Value <= MaxSafeInteger;
                }
            }

            public override double? AsFloatingPoint()
            {
                return this.lazyNumber.Value;
            }

            public override long? AsInteger()
            {
                return (long)this.lazyNumber.Value;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                if (this.IsFloatingPoint)
                {
                    jsonWriter.WriteNumberValue(this.AsFloatingPoint().Value);
                }
                else
                {
                    jsonWriter.WriteIntValue(this.AsInteger().Value);
                }
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
