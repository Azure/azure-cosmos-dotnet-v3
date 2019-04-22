//-----------------------------------------------------------------------
// <copyright file="CosmosString.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal static class CosmosString
    {
        public static CosmosElement Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new CosmosTypedElement<string>.LazyCosmosTypedElement(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.String,
                (navigator, node) => navigator.GetStringValue(node),
                CosmosElementType.String);
        }

        public static CosmosElement Create(string value)
        {
            return new CosmosTypedElement<string>.EagerTypedElement(
                value,
                CosmosElementType.String,
                ((stringValue, writer) => writer.WriteStringValue(stringValue)));
        }
    }
}
