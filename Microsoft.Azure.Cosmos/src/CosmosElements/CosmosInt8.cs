// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal static class CosmosInt8
    {
        public static CosmosElement Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new CosmosTypedElement<sbyte>.LazyCosmosTypedElement(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.Int8,
                (navigator, node) => navigator.GetInt8Value(node),
                CosmosElementType.Int8);
        }

        public static CosmosElement Create(sbyte value)
        {
            return new CosmosTypedElement<sbyte>.EagerTypedElement(
                value,
                CosmosElementType.Int8,
                ((intValue, writer) => writer.WriteInt8Value(intValue)));
        }
    }
}