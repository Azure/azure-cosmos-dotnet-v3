//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMJavaScriptLiteral : ClientQLLiteral
    {
        public ClientQLMJavaScriptLiteral(string name)
            : base(ClientQLLiteralKind.MJavaScript)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}