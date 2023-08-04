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
        private static Dictionary<string, string> normalizedToCosmosDBRegionNameMapping;

        internal static void PrepareCache()
        {
            FieldInfo[] fields = typeof(Regions).GetFields(BindingFlags.Public | BindingFlags.Static);
            normalizedToCosmosDBRegionNameMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (FieldInfo field in fields)
            {
                normalizedToCosmosDBRegionNameMapping[field.Name.ToLowerInvariant()] = field.GetValue(null).ToString();
            }
        }

        internal static void ClearCache()
        {
            normalizedToCosmosDBRegionNameMapping = null;
        }

        /// <summary>
        /// Given a normalized region name, this function retrieves the region name in the format that CosmosDB expects.
        /// If the region is not known, the same value as input is returned.
        /// </summary>
        /// <param name="normalizedRegionName">An Azure region name in a normalized format. The input is not case sensitive.</param>
        /// <returns>A string that contains the region name in the format that CosmosDB expects.</returns>
        public static string GetCosmosDBRegionName(string normalizedRegionName)
        {
            if (string.IsNullOrEmpty(normalizedRegionName))
            {
                return string.Empty;
            }

            if (normalizedToCosmosDBRegionNameMapping == null)
            {
                throw new ApplicationException("Name mapping cache has not been initialized");
            }

            if (normalizedToCosmosDBRegionNameMapping.TryGetValue(normalizedRegionName, out string cosmosDBRegionName))
            {
                return cosmosDBRegionName;
            }

            return normalizedRegionName;
        }
    }
}
