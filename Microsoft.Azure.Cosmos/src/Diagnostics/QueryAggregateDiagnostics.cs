//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    internal sealed class QueryAggregateDiagnostics : CosmosDiagnostics
    {
        private const string EmptyJsonArray = "[]";

        public QueryAggregateDiagnostics(
            IReadOnlyCollection<QueryPageDiagnostics> pages)
        {
            if (pages == null)
            {
                throw new ArgumentNullException(nameof(pages));
            }

            this.Pages = pages;
        }

        public IReadOnlyCollection<QueryPageDiagnostics> Pages { get; }

        public override string ToString()
        {
            if (this.Pages.Count == 0)
            {
                return QueryAggregateDiagnostics.EmptyJsonArray;
            }

            StringBuilder stringBuilder = new StringBuilder();

            // JSON array start
            stringBuilder.Append("[");

            foreach (QueryPageDiagnostics queryPage in this.Pages)
            {
                queryPage.AppendJson(stringBuilder);

                // JSON seperate objects
                stringBuilder.Append(",");
            }

            // JSON array stop
            stringBuilder.Length -= 1;
            stringBuilder.Append("]");
            return stringBuilder.ToString();
        }
    }
}
