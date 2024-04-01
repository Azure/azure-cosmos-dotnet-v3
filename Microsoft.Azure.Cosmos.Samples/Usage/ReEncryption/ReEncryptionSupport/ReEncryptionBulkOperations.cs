// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    internal class ReEncryptionBulkOperations<T>
    {
        internal List<Task<ReEncryptionOperationResponse<T>>> Tasks { get; }

        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public ReEncryptionBulkOperations(int operationCount)
        {
            this.Tasks = new List<Task<ReEncryptionOperationResponse<T>>>(operationCount);
        }

        public async Task<ReEncryptionBulkOperationResponse<T>> ExecuteAsync()
        {
            await Task.WhenAll(this.Tasks);
            this.stopwatch.Stop();
            return new ReEncryptionBulkOperationResponse<T>()
            {
                TotalTimeTaken = this.stopwatch.Elapsed,
                TotalRequestUnitsConsumed = this.Tasks.Sum(task => task.Result.RequestUnitsConsumed),
                SuccessfulDocumentCount = this.Tasks.Count(task => task.Result.IsSuccessful),
                FailedDocuments = this.Tasks
                    .Where(task => !task.Result.IsSuccessful)
                    .Select(task => (task.Result.Item, task.Result.CosmosException))
                    .ToList(),
            };
        }
    }
}