//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal static class OpenTelemetryHelper
    {
        /// <summary>
        /// Get comma separated list of regions contacted from the diagnostic
        /// </summary>
        /// <returns>Comma separated region list</returns>
        internal static string GetContactedRegions(IReadOnlyCollection<(string regionName, Uri uri)> regionList)
        {
            if (regionList == null || regionList.Count == 0)
            {
                return null;
            }

            if (regionList.Count == 1)
            {
                return regionList.ElementAt(0).regionName;
            }

            StringBuilder regionsContacted = new StringBuilder();
            foreach ((string name, _) in regionList)
            {
                if (regionsContacted.Length > 0)
                {
                    regionsContacted.Append(",");
                }
                regionsContacted.Append(name);
            }

            return regionsContacted.ToString();
        }

    }
}
