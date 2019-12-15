//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Common;

    internal class DekCache
    {
        private AsyncCache<Uri, InMemoryDekProperties> asyncCache = new AsyncCache<Uri, InMemoryDekProperties>();

        public void AddOrUpdate(InMemoryDekProperties dekProperties)
        {
        }

        public void Remove(Uri linkUri)
        {
        }

        /// <summary>
        /// Will strip off rawDek if ttl has expired.
        /// </summary>
        /// <param name="linkUri"></param>
        /// <returns>CachedDekProperties</returns>
        public InMemoryDekProperties Get(Uri linkUri)
        {
            return null;
        }
    }
}
