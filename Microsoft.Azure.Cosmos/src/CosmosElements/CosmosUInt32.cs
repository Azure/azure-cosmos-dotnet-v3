// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal static class CosmosUInt32
    {
        public static CosmosElement Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new CosmosTypedElement<uint>.LazyCosmosTypedElement(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.UInt32,
                (navigator, node) => navigator.GetUInt32Value(node),
                CosmosElementType.UInt32);
        }

        public static CosmosElement Create(uint value)
        {
            return new CosmosTypedElement<uint>.EagerTypedElement(
                value,
                CosmosElementType.UInt32,
                ((intValue, writer) => writer.WriteUInt32Value(intValue)));
        }
    }
}