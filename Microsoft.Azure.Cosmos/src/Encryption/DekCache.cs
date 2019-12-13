//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Common;

    internal class DekCache
    {
        private AsyncCache<string, DataEncryptionKeyProperties> asyncCache = new AsyncCache<string, DataEncryptionKeyProperties>();

        public void AddOrUpdate(CachedDekProperties dekProperties)
        {
        }

        public void Remove(DataEncryptionKeyProperties dekProperties)
        {
        }

        public CachedDekProperties Get(string id)
        {
            return null;
        }
    }
}
