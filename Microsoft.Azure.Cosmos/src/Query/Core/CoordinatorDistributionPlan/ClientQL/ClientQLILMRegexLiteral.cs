//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLILMRegexLiteral : ClientQLLiteral
    {
        public ClientQLILMRegexLiteral(string strPatter, string strOption)
            : base(ClientQLLiteralKind.MRegex)
        {
            this.StrPatter = strPatter;
            this.StrOption = strOption;
        }

        public string StrPatter { get; }
        
        public string StrOption { get; }
    }
}