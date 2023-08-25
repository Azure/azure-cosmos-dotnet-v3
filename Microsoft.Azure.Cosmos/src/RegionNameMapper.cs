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
    internal sealed class RegionNameMapper
    {
        private readonly Dictionary<string, string> normalizedToCosmosDBRegionNameMapping;

        public RegionNameMapper()
        {
            FieldInfo[] fields = typeof(Regions).GetFields(BindingFlags.Public | BindingFlags.Static);

            this.normalizedToCosmosDBRegionNameMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (FieldInfo field in fields)
            {
                Console.WriteLine($"{field.Name} -> {field.GetValue(null)}");
                this.normalizedToCosmosDBRegionNameMapping[field.Name] = field.GetValue(null).ToString();
            }
        }

        /// <summary>
        /// Given a normalized region name, this function retrieves the region name in the format that CosmosDB expects.
        /// If the region is not known, the same value as input is returned.
        /// </summary>
        /// <param name="normalizedRegionName">An Azure region name in a normalized format. The input is not case sensitive.</param>
        /// <returns>A string that contains the region name in the format that CosmosDB expects.</returns>
        public string GetCosmosDBRegionName(string normalizedRegionName)
        {
            if (string.IsNullOrEmpty(normalizedRegionName))
            {
                return string.Empty;
            }

            normalizedRegionName = normalizedRegionName.Replace(" ", string.Empty);
            if (this.normalizedToCosmosDBRegionNameMapping.TryGetValue(normalizedRegionName,
                out string cosmosDBRegionName))
            {
                return cosmosDBRegionName;
            }

            return normalizedRegionName;
        }
    }
}
