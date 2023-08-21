//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLObjectLiteralProperty
    {
        public ClientQLObjectLiteralProperty(string strName, ClientQLLiteral literal)
        {
            this.StrName = strName;
            this.Literal = literal;
        }

        public string StrName { get; }

        public ClientQLLiteral Literal { get; }
    }
}