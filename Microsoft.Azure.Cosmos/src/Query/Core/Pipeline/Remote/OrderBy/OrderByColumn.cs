// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote.OrderBy
{
    using System;

    internal readonly struct OrderByColumn
    {
        public OrderByColumn(string expression, SortOrder sortOrder)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.SortOrder = sortOrder;
        }

        public string Expression { get; }
        public SortOrder SortOrder { get; }
    }
}
