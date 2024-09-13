//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json.Serialization;

    internal static class CosmosSerializationUtil
    {
        private static readonly CamelCaseNamingStrategy camelCaseNamingStrategy = new CamelCaseNamingStrategy();

        internal static string GetStringWithPropertyNamingPolicy(CosmosLinqSerializerOptions options, string name)
        {
            if (options == null)
            {
                return name;
            }

            return GetStringWithPropertyNamingPolicy(options.PropertyNamingPolicy, name);
        }

        internal static string GetStringWithPropertyNamingPolicy(CosmosPropertyNamingPolicy namingPolicy, string name)
        {
            return namingPolicy switch
            {
                CosmosPropertyNamingPolicy.CamelCase => CosmosSerializationUtil.camelCaseNamingStrategy.GetPropertyName(name, false),
                CosmosPropertyNamingPolicy.Default => name,
                _ => throw new NotImplementedException("Unsupported CosmosPropertyNamingPolicy value"),
            };
        }
    }
}
