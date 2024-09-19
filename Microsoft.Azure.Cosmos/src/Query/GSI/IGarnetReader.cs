//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal interface IGarnetReader
    {
        public string HashGet(string key1, string key2);

        public Task<List<string>> BatchHashGetAsync(List<Tuple<string, string>> secondaryIndexTermAndPartitionKeyRangeId);

        public Task<string> GetStatsAsync();

        public Task<List<string>> GetPartitionIdsWithSecondaryIndexTermAsync(string term);
    }
}
