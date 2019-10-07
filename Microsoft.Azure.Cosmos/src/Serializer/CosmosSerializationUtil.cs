//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json.Serialization;

    internal static class CosmosSerializationUtil
    {
        private static CamelCaseNamingStrategy camelCaseNamingStrategy = new CamelCaseNamingStrategy();

        internal static string ToCamelCase(string name)
        {
            return CosmosSerializationUtil.camelCaseNamingStrategy.GetPropertyName(name, false);
        }

        internal static string GetStringWithPropertyNamingPolicy(CosmosSerializationOptions options, string name)
        {
            if (options != null && options.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase)
            {
                return CosmosSerializationUtil.ToCamelCase(name);
            }

            return name;
        }
    }
}
