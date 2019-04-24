//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal class CosmosConflictCore : CosmosConflict
    {
        /// <summary>
        /// Gets the operation that resulted in the conflict in the Azure Cosmos DB service.
        /// </summary>
        public override OperationKind OperationKind { get; }

        /// <summary>
        /// Gets the type of the conflicting resource in the Azure Cosmos DB service.
        /// </summary>
        public override Type ResourceType { get; }

        public override string Id { get; }
    }
}