//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLMJavaScriptLiteral : QLLiteral
    {
        public QLMJavaScriptLiteral(string name)
            : base(QLLiteralKind.MJavaScript)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}