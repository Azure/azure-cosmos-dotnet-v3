//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// The typed response that contains the current, previous, and metadata change feed resource when <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.FullFidelity"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif  
        class ItemChanges<T>
    {
        /// <summary>
        /// New instance of ItemChanges that contain change feed resource along with metadata.
        /// </summary>
        /// <param name="current"></param>
        /// <param name="changeFeedMetadata"></param>
        /// <param name="previous"></param>
        public ItemChanges(T current, ChangeFeedMetadata changeFeedMetadata, T previous)
        {
            this.Current = current;
            this.Metadata = changeFeedMetadata;
            this.Previous = previous;
        }

        /// <summary>
        /// The full fidelity change feed current item.
        /// </summary>
        public T Current { get; set; }

        /// <summary>
        /// The full fidelity change feed metadata.
        /// </summary>
        public ChangeFeedMetadata Metadata { get; set; }

        /// <summary>
        /// The full fidelity change feed previous item.
        /// </summary>
        public T Previous { get; set; }
    }
}
