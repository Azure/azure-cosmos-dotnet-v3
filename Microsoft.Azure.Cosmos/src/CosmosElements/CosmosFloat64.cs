// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal static class CosmosFloat64
    {
        public static CosmosElement Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new CosmosTypedElement<double>.LazyCosmosTypedElement(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.Float64,
                (navigator, node) => navigator.GetFloat64Value(node),
                CosmosElementType.Float64);
        }

        public static CosmosElement Create(double value)
        {
            return new CosmosTypedElement<double>.EagerTypedElement(
                value,
                CosmosElementType.Float64,
                ((floatValue, writer) => writer.WriteFloat64Value(floatValue)));
        }
    }
}