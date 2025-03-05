// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;

    internal sealed class BufferedOrderByResults
    {
        public IEnumerator<OrderByQueryResult> Enumerator { get; }

        public int Count { get; }

        public double TotalRequestCharge { get; }

        public QueryPageParameters QueryPageParameters { get; }

        public BufferedOrderByResults(
            IEnumerator<OrderByQueryResult> enumerator,
            int itemCount,
            double totalRequestCharge,
            QueryPageParameters queryPageParameters)
        {
            this.Enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            this.Count = itemCount;
            this.TotalRequestCharge = totalRequestCharge;
            this.QueryPageParameters = queryPageParameters ?? throw new ArgumentNullException(nameof(queryPageParameters));
        }
    }
}
