//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLUserDefinedFunctionCallScalarExpression : ClientQLScalarExpression
    {
        public ClientQLFunctionIdentifier Identifier { get; set; }

        public List<ClientQLScalarExpression> VecArguments { get; set; }
        
        public bool Builtin { get; set; }
    }

}