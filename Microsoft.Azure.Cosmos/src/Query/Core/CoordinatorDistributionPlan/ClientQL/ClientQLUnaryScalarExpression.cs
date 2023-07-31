//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLUnaryScalarExpression : ClientQLScalarExpression
    {
        public ClientQLUnaryScalarOperatorKind OperatorKind { get; set; }
        
        public ClientQLScalarExpression Expression { get; set; }
    }

}