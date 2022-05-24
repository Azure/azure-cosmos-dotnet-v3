//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// The metadata of a change feed resource with <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.FullFidelity"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif 
        class ChangeFeedMetadata
    {
        /// <summary>
        /// New instance of meta data for <see cref="ChangeFeedItemChanges{T}"/> created.
        /// </summary>
        /// <param name="conflictResolutionTimestamp"></param>
        /// <param name="currentLogSequenceNumber"></param>
        /// <param name="operationType"></param>
        /// <param name="previousLogSequenceNumber"></param>
        public ChangeFeedMetadata(
            DateTime conflictResolutionTimestamp, 
            long currentLogSequenceNumber, 
            ChangeFeedOperationType operationType, 
            long previousLogSequenceNumber)
        {
            this.ConflictResolutionTimestamp = conflictResolutionTimestamp;
            this.CurrentLogSequenceNumber = currentLogSequenceNumber;
            this.OperationType = operationType;
            this.PreviousLogSequenceNumber = previousLogSequenceNumber;
        }

        /// <summary>
        /// The conflict resolution timestamp.
        /// </summary>
        public DateTime ConflictResolutionTimestamp { get; }

        /// <summary>
        /// The current logical sequence number.
        /// </summary>
        public long CurrentLogSequenceNumber { get; }

        /// <summary>
        /// The change feed operation type.
        /// </summary>
        public ChangeFeedOperationType OperationType { get; }

        /// <summary>
        /// The previous logical sequence number.
        /// </summary>
        public long PreviousLogSequenceNumber { get; }
    }
}
