//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Maps a normalized region name to the format that CosmosDB is expecting (for e.g. from 'westus2' to 'West US 2')
    /// </summary>
    internal class RegionNameMapping
    {
        private static readonly IReadOnlyDictionary<string, string> normalizedToCosmosDBRegionNameMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"westus", Regions.WestUS},
                {"westus2", Regions.WestUS2},
                {"westcentralus", Regions.WestCentralUS},
                {"eastus", Regions.EastUS},
                {"eastus2", Regions.EastUS2},
                {"centralus", Regions.CentralUS},
                {"southcentralus", Regions.SouthCentralUS},
                {"northcentralus", Regions.NorthCentralUS},
                {"westeurope", Regions.WestEurope},
                {"northeurope", Regions.NorthEurope},
                {"eastasia", Regions.EastAsia},
                {"southeastasia", Regions.SoutheastAsia},
                {"japaneast", Regions.JapanEast},
                {"japanwest", Regions.JapanWest},
                {"australiaeast", Regions.AustraliaEast},
                {"australiasoutheast", Regions.AustraliaSoutheast},
                {"centralindia", Regions.CentralIndia},
                {"southindia", Regions.SouthIndia},
                {"westindia", Regions.WestIndia},
                {"canadaeast", Regions.CanadaEast},
                {"canadacentral", Regions.CanadaCentral},
                {"germanycentral", Regions.GermanyCentral},
                {"germanynortheast", Regions.GermanyNortheast},
                {"chinanorth", Regions.ChinaNorth},
                {"chinaeast", Regions.ChinaEast},
                {"chinanorth2", Regions.ChinaNorth2},
                {"chinaeast2", Regions.ChinaEast2},
                {"koreasouth", Regions.KoreaSouth},
                {"koreacentral", Regions.KoreaCentral},
                {"ukwest", Regions.UKWest},
                {"uksouth", Regions.UKSouth},
                {"brazilsouth", Regions.BrazilSouth},
                {"usgovarizona", Regions.USGovArizona},
                {"usgovtexas", Regions.USGovTexas},
                {"usgovvirginia", Regions.USGovVirginia},
                {"eastus2euap", Regions.EastUS2EUAP},
                {"centraluseuap", Regions.CentralUSEUAP},
                {"francecentral", Regions.FranceCentral},
                {"francesouth", Regions.FranceSouth},
                {"usdodcentral", Regions.USDoDCentral},
                {"usdodeast", Regions.USDoDEast},
                {"australiacentral", Regions.AustraliaCentral},
                {"australiacentral2", Regions.AustraliaCentral2},
                {"southafricanorth", Regions.SouthAfricaNorth},
                {"southafricawest", Regions.SouthAfricaWest},
                {"uaecentral", Regions.UAECentral},
                {"uaenorth", Regions.UAENorth},
                {"usnateast", Regions.USNatEast},
                {"usnatwest", Regions.USNatWest},
                {"usseceast", Regions.USSecEast},
                {"ussecwest", Regions.USSecWest},
                {"switzerlandnorth", Regions.SwitzerlandNorth},
                {"switzerlandwest", Regions.SwitzerlandWest},
                {"germanynorth", Regions.GermanyNorth},
                {"germanywestcentral", Regions.GermanyWestCentral},
                {"norwayeast", Regions.NorwayEast},
                {"norwaywest", Regions.NorwayWest},
                {"brazilsoutheast", Regions.BrazilSoutheast},
                {"westus3", Regions.WestUS3},
                {"jioindiacentral", Regions.JioIndiaCentral},
                {"jioindiawest", Regions.JioIndiaWest},
                {"eastusslv", Regions.EastUSSLV},
                {"swedencentral", Regions.SwedenCentral},
                {"swedensouth", Regions.SwedenSouth},
                {"qatarcentral", Regions.QatarCentral},
                {"chinanorth3", Regions.ChinaNorth3},
                {"chinaeast3", Regions.ChinaEast3},
                {"polandcentral", Regions.PolandCentral},
            };

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
