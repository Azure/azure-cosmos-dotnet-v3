//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLILMRegexLiteral : ClientQLLiteral
    {
        public string StrPatter { get; set; }
        
        public string StrOption { get; set; }
    }
}