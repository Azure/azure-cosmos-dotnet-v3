//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Specifies the possible states of a <see cref="Snapshot"/> resource in the Azure Cosmos DB service.
    /// </summary>
    internal enum SnapshotState
    {
        /// <summary>
        /// Represents snapshots that are in the process of being created.
        /// </summary>
        Pending,

        /// <summary>
        /// Represents snapshots that are completely created and ready to be read.
        /// </summary>
        Completed,

        /// <summary>
        /// Represents snapshots that failed to be created.
        /// </summary>
        Failed,

        Invalid
    }
}