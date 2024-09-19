//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;

    internal static class GSIFactory
    {
       public static IGarnetReader cacheReader = new GarnetReader();

       public static async Task FilterUsingCacheAsync(SqlQuerySpec querySpec, List<Documents.PartitionKeyRange> keyRanges)
       {
            List<string> pkrangesIdsToKeep = new ();
            foreach (var parameter in querySpec.Parameters)
            {
                if (parameter.Name.Contains("emailId1"))
                {
                    pkrangesIdsToKeep = await cacheReader.GetPartitionIdsWithSecondaryIndexTermAsync(Convert.ToString(parameter.Value));
                }
            }

            keyRanges = keyRanges.Where(obj => pkrangesIdsToKeep.Contains(obj.Id)).ToList();
        }
    }
}
