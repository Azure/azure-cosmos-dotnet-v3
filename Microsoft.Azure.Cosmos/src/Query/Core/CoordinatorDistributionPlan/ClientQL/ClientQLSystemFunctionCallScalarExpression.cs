//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLSystemFunctionCallScalarExpression : ClientQLScalarExpression
    {
        public ClientQLSystemFunctionCallScalarExpression(ClientQLBuiltinScalarFunctionKind functionKind, IReadOnlyList<ClientQLScalarExpression> arguments) 
            : base(ClientQLScalarExpressionKind.SystemFunctionCall)
        {
            this.FunctionKind = functionKind;
            this.Arguments = arguments;
        }

        public ClientQLBuiltinScalarFunctionKind FunctionKind { get; }
        
        public IReadOnlyList<ClientQLScalarExpression> Arguments { get; }
    }

}