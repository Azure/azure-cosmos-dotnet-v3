//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLSystemFunctionCallScalarExpression : ClientQLScalarExpression
    {
        public ClientQLBuiltinScalarFunctionKind FunctionKind { get; set; }
        
        public List<ClientQLScalarExpression> VecArguments { get; set; }
    }

}