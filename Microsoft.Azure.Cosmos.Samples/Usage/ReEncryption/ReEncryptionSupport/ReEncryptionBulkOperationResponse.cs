// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// ReEncryption Bulk Operation Response.
    /// </summary>
    /// <typeparam name="T">  Type </typeparam>
    public sealed class ReEncryptionBulkOperationResponse<T>
    {
        /// <summary>
        /// Gets total operation time.
        /// </summary>
        public TimeSpan TotalTimeTaken { get; internal set; }

        /// <summary>
        /// Gets total Request Units consumed for this bulk operation.
        /// </summary>
        public double TotalRequestUnitsConsumed { get; internal set; } = 0;

        /// <summary>
        /// Gets total Documents Successfully reencrypted.
        /// </summary>
        public int SuccessfulDocumentCount { get; internal set; } = 0;

        /// <summary>
        /// Gets list of all failures and returns the document and corresponding exception.
        /// </summary>
        public IReadOnlyList<(T, Exception)> FailedDocuments { get; internal set; }
    }
}