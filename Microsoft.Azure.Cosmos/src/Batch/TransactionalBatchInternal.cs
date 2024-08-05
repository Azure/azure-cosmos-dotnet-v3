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

    internal abstract class TransactionalBatchInternal : TransactionalBatch
    {
        protected List<ItemBatchOperation> operations;

        internal bool isHomogenousOperations = false;

        internal ItemBatchOperation lastItemBatchOperation = null;

        protected void AddOperation(ItemBatchOperation itemBatchOperation)
        {
            this.operations.Add(itemBatchOperation);
            if (this.operations.Count == 1)
            {
                this.lastItemBatchOperation = itemBatchOperation;
            }
            else
            {
                this.isHomogenousOperations = this.isHomogenousOperations 
                    && this.lastItemBatchOperation.OperationType == itemBatchOperation.OperationType;
                this.lastItemBatchOperation = itemBatchOperation;
            }
        }
    }
}
