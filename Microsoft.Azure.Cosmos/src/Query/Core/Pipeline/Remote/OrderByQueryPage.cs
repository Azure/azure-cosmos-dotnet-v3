// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class OrderByQueryPage : Page<QueryState>
    {
        public OrderByQueryPage(QueryPage queryPage)
            : base(queryPage.State)
        {
            this.Enumerator = queryPage.Documents.GetEnumerator();
        }

        public QueryPage Page { get; }

        public IEnumerator<CosmosElement> Enumerator { get; }
    }
}
