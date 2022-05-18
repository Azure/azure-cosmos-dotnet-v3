//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// The metadata of a change feed resource with <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.AllOperations"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif 
        class ChangeFeedMetadata
    {
        /// <summary>
        /// ctor.
        /// </summary>
        /// <param name="crts"></param>
        /// <param name="currentLSN"></param>
        /// <param name="operationType"></param>
        /// <param name="previousLSN"></param>
        public ChangeFeedMetadata(
            DateTime crts, 
            long currentLSN, 
            ChangeFeedOperationType operationType, 
            long previousLSN)
        {
            this.CRTS = crts;
            this.CurrentLSN = currentLSN;
            this.OperationType = operationType;
            this.PreviousLSN = previousLSN;
        }

        /// <summary>
        /// The conflict resolution timestamp.
        /// </summary>
        public DateTime CRTS { get; set; }

        /// <summary>
        /// The current logical sequence number.
        /// </summary>
        public long CurrentLSN { get; set; }

        /// <summary>
        /// The change feed operation type.
        /// </summary>
        internal ChangeFeedOperationType OperationType { get; set; }

        /// <summary>
        /// The previous logical sequence number.
        /// </summary>
        public long PreviousLSN { get; set; }
    }
}
