// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.ChangeFeed;

    /// <summary>
    /// Base class for the change feed mode <see cref="ChangeFeedRequestOptions"/>.
    /// </summary>
    /// <remarks>Use one of the static constructors to generate a ChangeFeedMode option.</remarks>
#if PREVIEW
    public
#else
    internal
#endif
    abstract class ChangeFeedMode
    {
        /// <summary>
        /// Initializes an instance of the <see cref="ChangeFeedMode"/> class.
        /// </summary>
        internal ChangeFeedMode()
        {
            // Internal so people can't derive from this type.
        }

        internal abstract void Accept(RequestMessage requestMessage);

        /// <summary>
        /// Creates a <see cref="ChangeFeedMode"/> to receive incremental item changes.
        /// </summary>
        /// <remarks>
        /// Incremental mode includes item creations and updates, not deletions.
        /// </remarks>
        /// <returns>A <see cref="ChangeFeedMode"/>  to receive incremental item changes.</returns>
        public static ChangeFeedMode Incremental => ChangeFeedModeIncremental.Instance;

        /// <summary>
        /// Creates a <see cref="ChangeFeedMode"/> to receive notifications for creations, deletes, as well as all intermediary snapshots for updates.
        /// </summary>
        /// <remarks>
        /// A container with a <see cref="ChangeFeedPolicy"/> configured is required. The delete operations will be included only within the configured retention period.
        /// When enabling full fidelity mode you will only be able to process change feed events within the retention window configured in the change feed policy of the container.
        /// If you attempt to process a change feed after more than the retention window an error(Status Code 400) will be returned because the events for intermediary
        /// updates and deletes have vanished.
        /// It would still be possible to process changes using <see cref="ChangeFeedMode.Incremental"/> mode even when configuring a full fidelity change feed policy 
        /// with retention window on the container and when using Incremental mode it doesn't matter whether your are out of the retention window or not -
        /// but no events for deletes or intermediary updates would be included.
        /// </remarks>
        /// <returns>A <see cref="ChangeFeedMode"/>  to receive notifications for insertions, updates, and delete operations.</returns>
        public static ChangeFeedMode FullFidelity => ChangeFeedModeFullFidelity.Instance;
    }
}
