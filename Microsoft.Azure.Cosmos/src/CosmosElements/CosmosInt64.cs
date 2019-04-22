// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal static class CosmosInt64
    {
        public static CosmosElement Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new CosmosTypedElement<long>.LazyCosmosTypedElement(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.Int64,
                (navigator, node) => navigator.GetInt64Value(node),
                CosmosElementType.Int64);
        }

        public static CosmosElement Create(long value)
        {
            return new CosmosTypedElement<long>.EagerTypedElement(
                value,
                CosmosElementType.Int64,
                ((intValue, writer) => writer.WriteInt64Value(intValue)));
        }
    }
}