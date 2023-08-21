//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLSystemFunctionCallScalarExpression : ClientQLScalarExpression
    {
        public ClientQLSystemFunctionCallScalarExpression(ClientQLBuiltinScalarFunctionKind functionKind, List<ClientQLScalarExpression> vecArguments) 
            : base(ClientQLScalarExpressionKind.SystemFunctionCall)
        {
            this.FunctionKind = functionKind;
            this.VecArguments = vecArguments;
        }

        public ClientQLBuiltinScalarFunctionKind FunctionKind { get; }
        
        public List<ClientQLScalarExpression> VecArguments { get; }
    }

}