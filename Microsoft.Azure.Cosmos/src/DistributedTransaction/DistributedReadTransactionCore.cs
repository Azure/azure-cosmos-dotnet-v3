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
            "CommitTransactionAsync has already been called on this transaction instance. " +
            "A DistributedReadTransaction is single-use; to retry, construct a new " +
            "DistributedReadTransaction with the same items.";

        private readonly CosmosClientContext clientContext;
        private readonly List<DistributedTransactionOperation> operations;
        private int isCommitInvoked;

        internal DistributedReadTransactionCore(CosmosClientContext clientContext)
        {
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.operations = new List<DistributedTransactionOperation>();
        }

        public override DistributedReadTransaction ReadItem(
            Container container,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            (string databaseId, string containerId) = DistributedTransactionConstants.ValidateAndUnpackContainer(container, this.clientContext.Client);
            DistributedReadTransactionCore.ValidateItemId(id);

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Read,
                    operationIndex: this.operations.Count,
                    database: databaseId,
                    container: containerId,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions));

            return this;
        }

        /// <inheritdoc/>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled before or during the commit.</exception>
        public override Task<DistributedTransactionResponse> CommitTransactionAsync(
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref this.isCommitInvoked, DistributedTransactionConstants.CommitStarted, DistributedTransactionConstants.CommitNotStarted) != DistributedTransactionConstants.CommitNotStarted)
            {
                throw new InvalidOperationException(CommitAlreadyCalledMessage);
            }

            return this.clientContext.OperationHelperAsync(
                operationName: $"{nameof(DistributedReadTransaction)}.{nameof(CommitTransactionAsync)}",
                containerName: null,
                databaseName: null,
                operationType: OperationType.CommitDistributedTransaction,
                requestOptions: null,
                task: (trace) =>
                {
                    DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                        operations: this.operations,
                        clientContext: this.clientContext);

                    return committer.CommitTransactionAsync(trace, cancellationToken);
                },
                openTelemetry: new (OpenTelemetryConstants.Operations.CommitDistributedReadTransaction,
                                    (response) => new OpenTelemetryResponse(response)),
                traceComponent: TraceComponent.Batch);
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
