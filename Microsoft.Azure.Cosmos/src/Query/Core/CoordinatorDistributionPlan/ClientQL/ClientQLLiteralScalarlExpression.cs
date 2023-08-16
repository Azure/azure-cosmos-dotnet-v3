//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLLiteralScalarlExpression : ClientQLScalarExpression
    {
        public ClientQLLiteral Literal { get; set; }
    }
}