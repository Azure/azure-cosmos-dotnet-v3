//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents an internal abstract class for handling transactional batches of operations.
    /// </summary>
    internal abstract class TransactionalBatchInternal : TransactionalBatch
    {
        /// <summary>
        /// The list of operations in the batch.
        /// </summary>
        protected List<ItemBatchOperation> operations;

        /// <summary>
        /// Indicates whether all operations in the batch are of the same type.
        /// </summary>
        internal bool isHomogenousOperations = true;

        /// <summary>
        /// The last operation added to the batch.
        /// </summary>
        internal ItemBatchOperation lastItemBatchOperation = null;

        /// <summary>
        /// Adds a new operation to the batch of operations and updates the homogeneity status of the operations.
        /// </summary>
        /// <param name="itemBatchOperation">The operation to be added to the batch.</param>
        /// <remarks>
        /// This method performs the following actions:
        /// <list type="number">
        /// <item>
        /// <description>Adds the given <paramref name="itemBatchOperation"/> to the operations list.</description>
        /// </item>
        /// <item>
        /// <description>If the added operation is the first operation in the batch, it sets this operation as the <see cref="lastItemBatchOperation"/>.</description>
        /// </item>
        /// <item>
        /// <description>If there are existing operations in the batch and the operations are currently homogeneous, it checks if the last added operation's type matches the new operation's type:
        /// <list type="bullet">
        /// <item><description>If they match, the batch remains homogeneous.</description></item>
        /// <item><description>If they do not match, the batch is no longer considered homogeneous.</description></item>
        /// </list>
        /// </description>
        /// </item>
        /// <item>
        /// <description>Updates the <see cref="lastItemBatchOperation"/> to the newly added operation.</description>
        /// </item>
        /// </list>
        /// </remarks>
        protected void AddOperation(ItemBatchOperation itemBatchOperation)
        {
            this.operations.Add(itemBatchOperation);
            if (this.operations.Count == 1)
            {
                this.lastItemBatchOperation = itemBatchOperation;
            }
            else if (this.isHomogenousOperations)
            {
                this.isHomogenousOperations = this.lastItemBatchOperation.OperationType == itemBatchOperation.OperationType;
                this.lastItemBatchOperation = itemBatchOperation;
            }
        }
    }
}
