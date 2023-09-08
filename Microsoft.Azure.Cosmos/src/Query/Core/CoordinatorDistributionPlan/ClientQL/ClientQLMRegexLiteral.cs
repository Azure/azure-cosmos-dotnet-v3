//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMRegexLiteral : ClientQLLiteral
    {
        public ClientQLMRegexLiteral(string pattern, string options)
            : base(ClientQLLiteralKind.MRegex)
        {
            this.Pattern = pattern;
            this.Options = options;
        }

        public string Pattern { get; }
        
        public string Options { get; }
    }
}