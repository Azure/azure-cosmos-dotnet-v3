// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal interface IQueryDataSource
    {
        public Task<TryCatch<QueryPage>> ExecuteQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            )
    }
}
