//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLTakeEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLTakeEnumerableExpression(ClientQLEnumerableExpression sourceExpression, int skipValue, int takeValue)
            : base(ClientQLEnumerableExpressionKind.Take)
        {
            this.SourceExpression = sourceExpression;
            this.SkipValue = skipValue;
            this.TakeValue = takeValue;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }

        public int SkipValue { get; }
        
        public int TakeValue { get; }
    }

}