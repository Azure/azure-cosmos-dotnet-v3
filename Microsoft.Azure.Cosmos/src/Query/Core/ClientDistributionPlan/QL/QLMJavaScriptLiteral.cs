//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLMJavaScriptLiteral : QLLiteral
    {
        public QLMJavaScriptLiteral(string name)
            : base(QLLiteralKind.MJavaScript)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
    }
}