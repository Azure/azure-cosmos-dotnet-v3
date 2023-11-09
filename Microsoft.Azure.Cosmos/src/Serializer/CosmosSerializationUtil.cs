//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json.Serialization;

    internal static class CosmosSerializationUtil
    {
        private static readonly CamelCaseNamingStrategy camelCaseNamingStrategy = new CamelCaseNamingStrategy();

        internal static string GetStringWithPropertyNamingPolicy(CosmosLinqSerializerOptions options, string name)
        {
            if (options != null && options.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase)
            {
                return CosmosSerializationUtil.camelCaseNamingStrategy.GetPropertyName(name, false);
            }

            return name;
        }

        internal static string GetStringWithPropertyNamingPolicy(CosmosPropertyNamingPolicy namingPolicy, string name)
        {
            if (namingPolicy == CosmosPropertyNamingPolicy.CamelCase)
            {
                return CosmosSerializationUtil.camelCaseNamingStrategy.GetPropertyName(name, false);
            }

            return name;
        }
    }
}
