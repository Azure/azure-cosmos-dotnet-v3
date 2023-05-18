//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Maps a normalized region name to the format that CosmosDB is expecting (for e.g. from 'westus2' to 'West US 2')
    /// </summary>
    internal class RegionNameMapping
    {
        private static readonly IDictionary<string, string> normalizedToCosmosDBRegionNameMapping;

        static RegionNameMapping()
        {
            FieldInfo[] fields = typeof(Regions).GetFields(BindingFlags.Public | BindingFlags.Static);
            normalizedToCosmosDBRegionNameMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (FieldInfo field in fields)
            {
                normalizedToCosmosDBRegionNameMapping[field.Name.ToLowerInvariant()] = field.GetValue(null).ToString();
            }
        }

        /// <summary>
        /// Given a normalized region name, this function retrieves the region name in the format that CosmosDB expects.
        /// If the region is not known, the same value as input is returned.
        /// </summary>
        /// <param name="normalizedRegionName">An Azure region name in a normalized format. The input is not case sensitive.</param>
        /// <returns>A string that contains the region name in the format that CosmosDB expects.</returns>
        public static string GetCosmosDBRegionName(string normalizedRegionName)
        {
            if (normalizedRegionName != null && normalizedToCosmosDBRegionNameMapping.TryGetValue(normalizedRegionName, out string cosmosDBRegionName))
            {
                return cosmosDBRegionName;
            }

            return normalizedRegionName;
        }
    }
}
