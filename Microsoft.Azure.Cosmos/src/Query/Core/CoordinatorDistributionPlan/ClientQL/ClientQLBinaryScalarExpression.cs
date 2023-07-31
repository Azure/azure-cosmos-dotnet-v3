//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLBinaryScalarExpression : ClientQLScalarExpression
    {
        public ClientQLBinaryScalarOperatorKind OperatorKind { get; set; }

        public int MaxDepth { get; set; }

        public ClientQLScalarExpression LeftExpression { get; set; }
        
        public ClientQLScalarExpression RightExpression { get; set; }
    }

}