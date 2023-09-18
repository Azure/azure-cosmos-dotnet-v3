//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLMRegexLiteral : QLLiteral
    {
        public QLMRegexLiteral(string pattern, string options)
            : base(QLLiteralKind.MRegex)
        {
            this.Pattern = pattern;
            this.Options = options;
        }

        public string Pattern { get; }
        
        public string Options { get; }
    }
}