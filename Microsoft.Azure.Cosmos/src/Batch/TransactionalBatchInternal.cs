//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents an internal abstract class for handling transactional batches of operations.
    /// This class is intended to be used as a base class for creating batches of operations 
    /// that can be executed transactionally in Azure Cosmos DB.
    /// </summary>
    internal abstract class TransactionalBatchInternal : TransactionalBatch
    {
    }
}
