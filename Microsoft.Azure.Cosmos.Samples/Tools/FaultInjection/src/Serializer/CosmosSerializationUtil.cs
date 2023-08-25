//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Serializer;
    using Newtonsoft.Json.Serialization;

    internal static class CosmosSerializationUtil
    {
        private static readonly CamelCaseNamingStrategy camelCaseNamingStrategy = new CamelCaseNamingStrategy();

        internal static string ToCamelCase(string name)
        {
            return CosmosSerializationUtil.camelCaseNamingStrategy.GetPropertyName(name, false);
        }

        internal static string GetStringWithPropertyNamingPolicy(CosmosLinqSerializerOptions options, string name)
        {
            if (options != null && options.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase)
            {
                return CosmosSerializationUtil.ToCamelCase(name);
            }

            return name;
        }
    }
}
