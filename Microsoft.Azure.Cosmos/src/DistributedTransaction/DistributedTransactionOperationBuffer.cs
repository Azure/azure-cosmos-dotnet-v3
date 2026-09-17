// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Threading;
    using Microsoft.Azure.Documents;

    internal sealed class DistributedTransactionOperationBuffer
    {
        private const int Building = 0;
        private const int Consumed = 1;
        private readonly List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>();
        private readonly object lifecycleLock = new object();
        private readonly string executionStartedMessage;
        private readonly string emptyTransactionMessage;
        private int executionState;

        internal DistributedTransactionOperationBuffer(
            string executionStartedMessage,
            string emptyTransactionMessage)
        {
            this.executionStartedMessage = executionStartedMessage ?? throw new ArgumentNullException(nameof(executionStartedMessage));
            this.emptyTransactionMessage = emptyTransactionMessage ?? throw new ArgumentNullException(nameof(emptyTransactionMessage));
        }

        internal void AddOperation(
            OperationType operationType,
            string databaseId,
            string containerId,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null,
            Stream streamPayload = null)
        {
            lock (this.lifecycleLock)
            {
                this.ThrowIfExecutionStarted();
                this.operations.Add(
                    new DistributedTransactionOperation(
                        operationType,
                        this.operations.Count,
                        databaseId,
                        containerId,
                        partitionKey,
                        id,
                        requestOptions)
                    {
                        ResourceStream = streamPayload
                    });
            }
        }

        internal void AddOperation<T>(
            OperationType operationType,
            string databaseId,
            string containerId,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            lock (this.lifecycleLock)
            {
                this.ThrowIfExecutionStarted();
                this.operations.Add(
                    new DistributedTransactionOperation<T>(
                        operationType,
                        this.operations.Count,
                        databaseId,
                        containerId,
                        partitionKey,
                        id,
                        resource,
                        requestOptions));
            }
        }

        internal IReadOnlyList<DistributedTransactionOperation> FreezeForExecution()
        {
            lock (this.lifecycleLock)
            {
                this.ThrowIfExecutionStarted();
                Volatile.Write(ref this.executionState, Consumed);
                if (this.operations.Count == 0)
                {
                    throw new InvalidOperationException(this.emptyTransactionMessage);
                }

                // Exclusive ownership and the consumed state keep this view stable without copying.
                return new ReadOnlyOperationCollection(this.operations);
            }
        }

        internal void ThrowIfExecutionStarted()
        {
            if (Volatile.Read(ref this.executionState) != Building)
            {
                throw new InvalidOperationException(this.executionStartedMessage);
            }
        }

        private sealed class ReadOnlyOperationCollection : ReadOnlyCollection<DistributedTransactionOperation>, ICollection
        {
            public ReadOnlyOperationCollection(List<DistributedTransactionOperation> operations)
                : base(operations)
            {
            }

            // The base implementation forwards SyncRoot to the mutable backing list on modern .NET.
            object ICollection.SyncRoot => this;
        }
    }
}
