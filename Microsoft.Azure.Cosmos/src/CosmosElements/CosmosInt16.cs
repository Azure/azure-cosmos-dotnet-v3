// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal static class CosmosInt16
    {
        public static CosmosElement Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new CosmosTypedElement<short>.LazyCosmosTypedElement(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.Int16,
                (navigator, node) => navigator.GetInt16Value(node),
                CosmosElementType.Int16);
        }

        public static CosmosElement Create(short value)
        {
            return new CosmosTypedElement<short>.EagerTypedElement(
                value,
                CosmosElementType.Int16,
                ((intValue, writer) => writer.WriteInt16Value(intValue)));
        }
    }
}