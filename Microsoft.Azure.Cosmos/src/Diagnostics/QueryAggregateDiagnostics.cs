//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;

    internal sealed class QueryAggregateDiagnostics : CosmosDiagnostics
    {
        public QueryAggregateDiagnostics(
            IReadOnlyCollection<QueryPageDiagnostics> pages)
        {
            if (pages == null)
            {
                throw new ArgumentNullException(nameof(pages));
            }

            this.Pages = pages;
        }

        private IReadOnlyCollection<QueryPageDiagnostics> Pages { get; }

        public override string ToString()
        {
            if (this.Pages.Count == 0)
            {
                return "[]";
            }

            StringBuilder stringBuilder = new StringBuilder();

            // JSON array start
            stringBuilder.Append("[");

            foreach (QueryPageDiagnostics queryPage in this.Pages)
            {
                queryPage.AppendString(stringBuilder);

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
