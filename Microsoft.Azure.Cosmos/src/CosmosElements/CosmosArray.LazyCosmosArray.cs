//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosArray : CosmosElement, IReadOnlyList<CosmosElement>
    {
        private sealed class LazyCosmosArray : CosmosArray
        {
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;

            public LazyCosmosArray(
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
                if (type != JsonNodeType.Array)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be an {JsonNodeType.Array} node. Got {type} instead.");
                }

                this.jsonNavigator = jsonNavigator;
                this.jsonNavigatorNode = jsonNavigatorNode;
            }

            public override int Count => this.jsonNavigator.GetArrayItemCount(this.jsonNavigatorNode);

            public override CosmosElement this[int index]
            {
                get
                {
                    IJsonNavigatorNode arrayItemNode = this.jsonNavigator.GetArrayItemAt(this.jsonNavigatorNode, index);
                    return CosmosElement.Dispatch(this.jsonNavigator, arrayItemNode);
                }
            }

            public override IEnumerator<CosmosElement> GetEnumerator() => this
                .jsonNavigator
                .GetArrayItems(this.jsonNavigatorNode)
                .Select((arrayItem) => CosmosElement.Dispatch(this.jsonNavigator, arrayItem))
                .GetEnumerator();

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteJsonNode(this.jsonNavigator, this.jsonNavigatorNode);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
