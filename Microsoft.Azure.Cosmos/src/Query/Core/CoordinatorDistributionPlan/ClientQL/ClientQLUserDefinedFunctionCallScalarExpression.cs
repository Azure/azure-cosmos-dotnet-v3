//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLUserDefinedFunctionCallScalarExpression : ClientQLScalarExpression
    {
        public ClientQLUserDefinedFunctionCallScalarExpression(ClientQLFunctionIdentifier identifier, IReadOnlyList<ClientQLScalarExpression> arguments, bool builtin) 
            : base(ClientQLScalarExpressionKind.UserDefinedFunctionCall)
        {
            this.Identifier = identifier;
            this.Arguments = arguments;
            this.Builtin = builtin;
        }

        public ClientQLFunctionIdentifier Identifier { get; }

        public IReadOnlyList<ClientQLScalarExpression> Arguments { get; }
        
        public bool Builtin { get; }
    }

}