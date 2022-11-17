//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Specifies the supported kinds of <see cref="Snapshot"/> resources in the Azure Cosmos DB service.
    /// </summary>
    internal enum SnapshotKind
    {
        /// <summary>
        /// Represents snapshots that were initiated by the user through a request to create a snapshot.
        /// </summary>
        OnDemand,

        /// <summary>
        /// Represents the current state of a container.
        /// </summary>
        Live,

        /// <summary>
        /// Represents the snapshotted state of a container whenever its partition map changes.
        /// </summary>
        System,

        Invalid
    }
}