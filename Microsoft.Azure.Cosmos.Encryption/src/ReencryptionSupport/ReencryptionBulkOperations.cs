// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    internal class ReencryptionBulkOperations<T>
    {
        internal List<Task<ReencryptionOperationResponse<T>>> Tasks { get; }

        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public ReencryptionBulkOperations(int operationCount)
        {
            this.Tasks = new List<Task<ReencryptionOperationResponse<T>>>(operationCount);
        }

        public async Task<ReencryptionBulkOperationResponse<T>> ExecuteAsync()
        {
            await Task.WhenAll(this.Tasks);
            this.stopwatch.Stop();
            return new ReencryptionBulkOperationResponse<T>()
            {
                TotalTimeTaken = this.stopwatch.Elapsed,
                TotalRequestUnitsConsumed = this.Tasks.Sum(task => task.Result.RequestUnitsConsumed),
                SuccessfulDocuments = this.Tasks.Count(task => task.Result.IsSuccessful),
                FailedDocuments = this.Tasks
                    .Where(task => !task.Result.IsSuccessful)
                    .Select(task => (task.Result.Item, task.Result.CosmosException))
                    .ToList(),
            };
        }
    }
}