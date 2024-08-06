//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents an internal abstract class for handling transactional batches of operations.
    /// This class is intended to be used as a base class for creating batches of operations 
    /// that can be executed transactionally in Azure Cosmos DB.
    /// </summary>
    internal abstract class TransactionalBatchInternal : TransactionalBatch
    {
        /// <summary>
        /// The list of operations in the batch.
        /// </summary>
        protected List<ItemBatchOperation> operations;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionalBatchInternal"/> class.
        /// </summary>
        public TransactionalBatchInternal()
        {
            this.operations = new List<ItemBatchOperation>();
        }

        /// <summary>
        /// Indicates whether all operations in the batch are of the same type.
        /// </summary>
        internal bool isHomogenousOperations = true;

        /// <summary>
        /// Stores the operation type if all operations in the batch are of the same type; otherwise, null.
        /// </summary>
        internal OperationType? homogenousOperation = null;

        /// <summary>
        /// Adds an operation to the batch.
        /// </summary>
        /// <param name="itemBatchOperation">The operation to add to the batch.</param>
        /// <remarks>
        /// This method performs the following actions:
        /// 1. Checks if the batch is homogeneous (all operations of the same type) and if the new operation's type matches the type of the existing operations.
        /// 2. Updates the <see cref="isHomogenousOperations"/> flag and the <see cref="homogenousOperation"/> property based on the check.
        /// 3. Adds the operation to the list of operations.
        /// </remarks>
        protected void AddOperation(ItemBatchOperation itemBatchOperation)
        {
            if (this.isHomogenousOperations && this.operations.Count > 0)
            {
                this.isHomogenousOperations = this.operations.First().OperationType == itemBatchOperation.OperationType;
                this.homogenousOperation = this.isHomogenousOperations ? itemBatchOperation.OperationType : null;
            }
            this.operations.Add(itemBatchOperation);
        }
    }
}
