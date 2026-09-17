// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class DistributedReadTransactionCore : DistributedReadTransaction
    {
        internal const string CommitAlreadyCalledMessage =
            "ExecuteTransactionAsync has already been called on this transaction instance. " +
            "A DistributedReadTransaction is single-use; to retry, construct a new " +
            "DistributedReadTransaction with the same items.";

        private readonly CosmosClientContext clientContext;
        private readonly DistributedTransactionOperationBuffer operationBuffer;
        private readonly object idempotencyTokenLock = new object();
        private Guid latestIdempotencyToken;

        internal DistributedReadTransactionCore(CosmosClientContext clientContext)
        {
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.operationBuffer = new DistributedTransactionOperationBuffer(
                CommitAlreadyCalledMessage,
                "Cannot commit a distributed read transaction with zero operations. This instance is consumed; construct a new DistributedReadTransaction and add at least one ReadItem call before executing.");
        }

        /// <inheritdoc/>
        internal override Guid IdempotencyToken
        {
            get
            {
                lock (this.idempotencyTokenLock)
                {
                    return this.latestIdempotencyToken;
                }
            }
        }

        public override DistributedReadTransaction ReadItem(
            Container container,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            this.operationBuffer.ThrowIfExecutionStarted();
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedReadTransactionCore.ValidateItemId(id);

            this.operationBuffer.AddOperation(
                operationType: OperationType.Read,
                databaseId,
                containerId,
                partitionKey,
                id,
                requestOptions: requestOptions);

            return this;
        }

        /// <inheritdoc/>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled before or during the commit.</exception>
        public override Task<DistributedTransactionResponse> ExecuteTransactionAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DistributedTransactionOperation> operations = this.operationBuffer.FreezeForExecution();

            return this.clientContext.OperationHelperAsync(
                operationName: $"{nameof(DistributedReadTransaction)}.{nameof(ExecuteTransactionAsync)}",
                containerName: null,
                databaseName: null,
                operationType: OperationType.Read,
                requestOptions: null,
                task: (trace) =>
                {
                    DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                        operations: operations,
                        clientContext: this.clientContext,
                        operationType: OperationType.Read,
                        onDispatch: this.PublishIdempotencyToken);

                    return committer.ExecuteTransactionAsync(trace, cancellationToken);
                },
                openTelemetry: new (OpenTelemetryConstants.Operations.ExecuteDistributedReadTransaction,
                                    (response) => new OpenTelemetryResponse(response)),
                traceComponent: TraceComponent.Batch);
        }

        private void PublishIdempotencyToken(Guid token)
        {
            lock (this.idempotencyTokenLock)
            {
                this.latestIdempotencyToken = token;
            }
        }

        private static void ValidateItemId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
        }
    }
}
