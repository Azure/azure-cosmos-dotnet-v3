﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLAggregate
    {
        public ClientQLAggregate(ClientQLAggregateKind kind, string operatorKind)
        {
            this.Kind = kind;
            this.OperatorKind = operatorKind;
        }

        public ClientQLAggregateKind Kind { get; }
        
        public string OperatorKind { get; }
    }
}