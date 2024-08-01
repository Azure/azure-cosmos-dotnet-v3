//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// The metadata of a change feed resource with <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.AllVersionsAndDeletes"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class ChangeFeedMetadata
    {
        /// <summary>
        /// New instance of meta data for <see cref="ChangeFeedItem{T}"/> created.
        /// </summary>
        /// <param name="crts"></param>
        /// <param name="lsn"></param>
        /// <param name="operationType"></param>
        /// <param name="previousImageLSN"></param>
        /// <param name="timeToLiveExpired"></param>
        public ChangeFeedMetadata(
            long crts,
            long lsn,
            ChangeFeedOperationType operationType,
            long previousImageLSN,
            bool timeToLiveExpired)
        {
            this.Crts = crts;
            this.Lsn = lsn;
            this.OperationType = operationType;
            this.PreviousImageLSN = previousImageLSN;
            this.TimeToLiveExpired = timeToLiveExpired;
        }

        /// <summary>
        /// The conflict resolution timestamp.
        /// </summary>
        public long Crts { get; }

        /// <summary>
        /// The current logical sequence number.
        /// </summary>
        public long Lsn { get; }

        /// <summary>
        /// The change feed operation type.
        /// </summary>
        public ChangeFeedOperationType OperationType { get; }

        /// <summary>
        /// The previous logical sequence number.
        /// </summary>
        public long PreviousImageLSN { get; }

        /// <summary>
        /// Used to distinquish explicit deletes (e.g. via DeleteItem) from deletes caused by TTL expiration (a collection may define time-to-live policy for documents).
        /// </summary>
        public bool TimeToLiveExpired { get; }
    }
}
