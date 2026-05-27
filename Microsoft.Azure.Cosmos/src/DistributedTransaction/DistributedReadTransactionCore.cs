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
        private readonly CosmosClientContext clientContext;
        private readonly List<DistributedTransactionOperation> operations;

        internal DistributedReadTransactionCore(CosmosClientContext clientContext)
        {
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.operations = new List<DistributedTransactionOperation>();
        }

        public override DistributedReadTransaction ReadItem(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null)
        {
            DistributedReadTransactionCore.ValidateContainerReference(database, collection);
            DistributedReadTransactionCore.ValidateItemId(id);

            this.operations.Add(
                new DistributedTransactionOperation(
                    operationType: OperationType.Read,
                    operationIndex: this.operations.Count,
                    database: database,
                    container: collection,
                    partitionKey: partitionKey,
                    id: id,
                    requestOptions: requestOptions));

            return this;
        }

        public override Task<DistributedTransactionResponse> CommitTransactionAsync(
            CancellationToken cancellationToken = default)
        {
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

        private static void ValidateContainerReference(string database, string collection)
        {
            if (string.IsNullOrWhiteSpace(database))
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (string.IsNullOrWhiteSpace(collection))
            {
                throw new ArgumentNullException(nameof(collection));
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
