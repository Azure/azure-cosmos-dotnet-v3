// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal static class CosmosInt32
    {
        public static CosmosElement Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new CosmosTypedElement<int>.LazyCosmosTypedElement(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.Int32,
                (navigator, node) => navigator.GetInt32Value(node),
                CosmosElementType.Int32);
        }

        public static CosmosElement Create(int value)
        {
            return new CosmosTypedElement<int>.EagerTypedElement(
                value,
                CosmosElementType.Int32,
                ((intValue, writer) => writer.WriteInt32Value(intValue)));
        }
    }
}