//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class InMemoryCollectionPage : Page<InMemoryCollectionState>
    {
        public InMemoryCollectionPage(List<InMemoryCollection.Record> records, InMemoryCollectionState state)
            : base(state)
        {
            this.Records = records;
        }

        public List<InMemoryCollection.Record> Records { get; }
    }
}
