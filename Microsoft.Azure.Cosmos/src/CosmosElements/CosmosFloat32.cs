// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal static class CosmosFloat32
    {
        public static CosmosElement Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new CosmosTypedElement<float>.LazyCosmosTypedElement(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.Float32,
                (navigator, node) => navigator.GetFloat32Value(node),
                CosmosElementType.Float32);
        }

        public static CosmosElement Create(float value)
        {
            return new CosmosTypedElement<float>.EagerTypedElement(
                value,
                CosmosElementType.Float32,
                ((floatValue, writer) => writer.WriteFloat32Value(floatValue)));
        }
    }
}