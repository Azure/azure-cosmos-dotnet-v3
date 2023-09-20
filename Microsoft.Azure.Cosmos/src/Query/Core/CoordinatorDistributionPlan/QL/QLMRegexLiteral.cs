//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLMRegexLiteral : QLLiteral
    {
        public QLMRegexLiteral(string pattern, string options)
            : base(QLLiteralKind.MRegex)
        {
            this.Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            this.Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public string Pattern { get; }
        
        public string Options { get; }
    }
}