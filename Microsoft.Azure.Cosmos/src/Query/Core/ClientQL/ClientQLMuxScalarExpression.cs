//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLMuxScalarExpression : ClientQLScalarExpression
    {
        public ClientQLScalarExpression ConditionExpression { get; set; }
        public ClientQLScalarExpression LeftExpression { get; set; }
        public ClientQLScalarExpression RightExpression { get; set; }
    }

}